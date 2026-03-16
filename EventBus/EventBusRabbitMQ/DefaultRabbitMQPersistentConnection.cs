using System.Diagnostics.CodeAnalysis;

namespace Lascodia.Trading.Engine.EventBusRabbitMQ;
[ExcludeFromCodeCoverage]
public class DefaultRabbitMQPersistentConnection
    : IRabbitMQPersistentConnection
{
    private readonly IConnectionFactory _connectionFactory;
    private readonly ILogger<DefaultRabbitMQPersistentConnection> _logger;
    private readonly int _retryCount;
    IConnection _connection;
    bool _disposed;
    private bool _isReconnecting;

    private readonly object sync_root = new object();

    public DefaultRabbitMQPersistentConnection(IConnectionFactory connectionFactory, ILogger<DefaultRabbitMQPersistentConnection> logger, int retryCount = 5)
    {
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _retryCount = retryCount;
    }

    public bool IsConnected
    {
        get
        {
            return _connection != null && _connection.IsOpen && !_disposed;
        }
    }

    public IModel CreateModel()
    {
        if (!IsConnected)
        {
            throw new InvalidOperationException("No RabbitMQ connections are available to perform this action");
        }

        return _connection.CreateModel();
    }

    public void Dispose()
    {
        if (_disposed) return;

        _disposed = true;

        if (_connection == null) return;

        try
        {
            // Unsubscribe from all events with individual try-catch blocks
            try
            {
                _connection.ConnectionShutdown -= OnConnectionShutdown;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error unsubscribing ConnectionShutdown during disposal");
            }

            try
            {
                _connection.CallbackException -= OnCallbackException;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error unsubscribing CallbackException during disposal");
            }

            try
            {
                _connection.ConnectionBlocked -= OnConnectionBlocked;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error unsubscribing ConnectionBlocked during disposal");
            }
        }
        finally
        {
            // Always try to dispose the connection
            try
            {
                _connection.Dispose();
            }
            catch (Exception ex)
            {
                _logger.LogCritical(ex, "Error disposing connection: {Message}", ex.Message);
            }
        }
    }

    public bool TryConnect()
    {
        _logger.LogInformation("RabbitMQ Client is trying to connect");

        lock (sync_root)
        {
            // Prevent multiple simultaneous reconnection attempts
            if (_isReconnecting)
            {
                _logger.LogInformation("Reconnection already in progress, skipping");
                return IsConnected;
            }

            // Check if already disposed
            if (_disposed)
            {
                _logger.LogWarning("Cannot connect - connection is disposed");
                return false;
            }

            _isReconnecting = true;

            try
            {
                // Dispose old connection properly to prevent memory leak
                if (_connection != null)
                {
                    IConnection oldConnection = _connection;
                    _connection = null; // Clear reference first to prevent reuse

                    try
                    {
                        // Ensure event handlers are unsubscribed even if Dispose fails
                        try
                        {
                            oldConnection.ConnectionShutdown -= OnConnectionShutdown;
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Error unsubscribing ConnectionShutdown");
                        }

                        try
                        {
                            oldConnection.CallbackException -= OnCallbackException;
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Error unsubscribing CallbackException");
                        }

                        try
                        {
                            oldConnection.ConnectionBlocked -= OnConnectionBlocked;
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Error unsubscribing ConnectionBlocked");
                        }
                    }
                    finally
                    {
                        // Always try to dispose the connection
                        try
                        {
                            oldConnection.Dispose();
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Error disposing old connection");
                        }
                    }
                }

                var policy = RetryPolicy.Handle<SocketException>()
                    .Or<BrokerUnreachableException>()
                    .WaitAndRetry(_retryCount, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)), (ex, time) =>
                    {
                        _logger.LogWarning(ex, "RabbitMQ Client could not connect after {TimeOut}s ({ExceptionMessage})", $"{time.TotalSeconds:n1}", ex.Message);
                    }
                );

                policy.Execute(() =>
                {
                    _connection = _connectionFactory
                            .CreateConnection();
                });

                if (IsConnected && !_disposed)
                {
                    // Defensive unsubscribe before subscribe to prevent double subscription
                    _connection.ConnectionShutdown -= OnConnectionShutdown;
                    _connection.CallbackException -= OnCallbackException;
                    _connection.ConnectionBlocked -= OnConnectionBlocked;

                    _connection.ConnectionShutdown += OnConnectionShutdown;
                    _connection.CallbackException += OnCallbackException;
                    _connection.ConnectionBlocked += OnConnectionBlocked;

                    _logger.LogInformation("RabbitMQ Client acquired a persistent connection to '{HostName}' and is subscribed to failure events", _connection.Endpoint.HostName);

                    return true;
                }
                else
                {
                    _logger.LogCritical("FATAL ERROR: RabbitMQ connections could not be created and opened");

                    return false;
                }
            }
            finally
            {
                _isReconnecting = false;
            }
        }
    }

    private void OnConnectionBlocked(object sender, ConnectionBlockedEventArgs e)
    {
        if (_disposed) return;

        _logger.LogWarning("A RabbitMQ connection is shutdown. Trying to re-connect...");

        TryConnect();
    }

    void OnCallbackException(object sender, CallbackExceptionEventArgs e)
    {
        if (_disposed) return;

        _logger.LogWarning("A RabbitMQ connection throw exception. Trying to re-connect...");

        TryConnect();
    }

    void OnConnectionShutdown(object sender, ShutdownEventArgs reason)
    {
        if (_disposed) return;

        _logger.LogWarning("A RabbitMQ connection is on shutdown. Trying to re-connect...");

        TryConnect();
    }
}
