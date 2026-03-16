using Lascodia.Trading.Engine.EventBus.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using System.Diagnostics.CodeAnalysis;

namespace Lascodia.Trading.Engine.EventBusRabbitMQ;

[ExcludeFromCodeCoverage]
public class EventBusRabbitMQ : IEventBus, IDisposable
{
    const string BROKER_NAME = "qtc_event_bus";

    private readonly IRabbitMQPersistentConnection _persistentConnection;
    private readonly ILogger<EventBusRabbitMQ> _logger;
    private readonly IEventBusSubscriptionsManager _subsManager;
    private readonly IServiceProvider _serviceProvider;
    private readonly int _retryCount;
    private IModel _consumerChannel;
    private string _queueName;
    private AsyncEventingBasicConsumer? _consumer;
    private bool _disposed;
    private EventHandler<CallbackExceptionEventArgs>? _channelCallbackExceptionHandler;

    public EventBusRabbitMQ(IRabbitMQPersistentConnection persistentConnection, ILogger<EventBusRabbitMQ> logger,
        IServiceProvider serviceProvider, IEventBusSubscriptionsManager subsManager, string? queueName, int retryCount = 5)
    {
        _persistentConnection = persistentConnection ?? throw new ArgumentNullException(nameof(persistentConnection));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _subsManager = subsManager ?? new InMemoryEventBusSubscriptionsManager();
        _queueName = queueName;
        _serviceProvider = serviceProvider;
        _retryCount = retryCount;
        _consumerChannel = CreateConsumerChannel();
        _subsManager.OnEventRemoved += SubsManager_OnEventRemoved;
    }

    ~EventBusRabbitMQ()
    {
        Dispose(false);
    }

    private void SubsManager_OnEventRemoved(object sender, string eventName)
    {
        if (!_persistentConnection.IsConnected)
        {
            _persistentConnection.TryConnect();
        }

        using var channel = _persistentConnection.CreateModel();
        channel.QueueUnbind(queue: _queueName,
            exchange: BROKER_NAME,
            routingKey: eventName);

        if (_subsManager.IsEmpty)
        {
            _queueName = string.Empty;
            if (_consumerChannel.IsOpen)
            {
                _consumerChannel.Close();
            }
        }
    }

    public void Publish(IntegrationEvent @event)
    {
        if (!_persistentConnection.IsConnected)
        {
            _persistentConnection.TryConnect();
        }

        var policy = RetryPolicy.Handle<BrokerUnreachableException>()
            .Or<SocketException>()
            .WaitAndRetry(_retryCount, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)), (ex, time) =>
            {
                _logger.LogWarning(ex, "Could not publish event: {EventId} after {Timeout}s ({ExceptionMessage})", @event.Id, $"{time.TotalSeconds:n1}", ex.Message);
            });

        var eventName = @event.GetType().Name;
        _logger.LogTrace("Creating RabbitMQ channel to publish event: {EventId} ({EventName})", @event.Id, eventName);

        using var channel = _persistentConnection.CreateModel();
        _logger.LogTrace("Declaring RabbitMQ exchange to publish event: {EventId}", @event.Id);
        channel.ExchangeDeclare(exchange: BROKER_NAME, type: "direct");
        var body = JsonSerializer.SerializeToUtf8Bytes(@event, @event.GetType(), new JsonSerializerOptions { WriteIndented = true });

        policy.Execute(() =>
        {
            var properties = channel.CreateBasicProperties();
            properties.DeliveryMode = 2; // persistent
            _logger.LogTrace("Publishing event to RabbitMQ: {EventId}", @event.Id);
            channel.BasicPublish(
                exchange: BROKER_NAME,
                routingKey: eventName,
                mandatory: true,
                basicProperties: properties,
                body: body);
        });
    }

    public void SubscribeDynamic<TH>(string eventName) where TH : IDynamicIntegrationEventHandler
    {
        _logger.LogInformation("Subscribing to dynamic event {EventName} with {EventHandler}", eventName, typeof(TH).GetGenericTypeName());
        DoInternalSubscription(eventName);
        _subsManager.AddDynamicSubscription<TH>(eventName);
        StartBasicConsume();
    }

    public void Subscribe<T, TH>() where T : IntegrationEvent where TH : IIntegrationEventHandler<T>
    {
        var eventName = _subsManager.GetEventKey<T>();
        DoInternalSubscription(eventName);
        _logger.LogInformation("Subscribing to event {EventName} with {EventHandler}", eventName, typeof(TH).GetGenericTypeName());
        _subsManager.AddSubscription<T, TH>();
        StartBasicConsume();
    }
    
    public void Subscribe(Type handler)
    {
        if (handler.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IIntegrationEventHandler<>)))
        {
            var eventType = handler.GetInterfaces().First(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IIntegrationEventHandler<>))
                .GetGenericArguments().First();
            var eventName = eventType.Name;
            DoInternalSubscription(eventName);
            _logger.LogInformation("Subscribing to event {EventName} with {EventHandler}", eventName, handler.GetGenericTypeName());
            _subsManager.AddSubscription(handler);
            StartBasicConsume();
        }
    }

    private void DoInternalSubscription(string eventName)
    {
        if (!_subsManager.HasSubscriptionsForEvent(eventName))
        {
            if (!_persistentConnection.IsConnected)
            {
                _persistentConnection.TryConnect();
            }
            _consumerChannel.QueueBind(queue: _queueName, exchange: BROKER_NAME, routingKey: eventName);
        }
    }

    public void Unsubscribe<T, TH>() where T : IntegrationEvent where TH : IIntegrationEventHandler<T>
    {
        var eventName = _subsManager.GetEventKey<T>();
        _logger.LogInformation("Unsubscribing from event {EventName}", eventName);
        _subsManager.RemoveSubscription<T, TH>();
    }

    public void UnsubscribeDynamic<TH>(string eventName) where TH : IDynamicIntegrationEventHandler
    {
        _subsManager.RemoveDynamicSubscription<TH>(eventName);
    }

    public void Unsubscribe(Type handlerType)
    {
        _subsManager.RemoveSubscription(handlerType);
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed) return;
        _disposed = true;

        if (disposing)
        {
            if (_consumer != null)
            {
                _consumer.Received -= Consumer_Received;
            }
            if (_consumerChannel != null)
            {
                // Unsubscribe from channel callback exception event to prevent memory leak
                if (_channelCallbackExceptionHandler != null)
                {
                    try
                    {
                        _consumerChannel.CallbackException -= _channelCallbackExceptionHandler;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Error unsubscribing from CallbackException during disposal");
                    }
                }
                _consumerChannel.Dispose();
            }
            _subsManager.OnEventRemoved -= SubsManager_OnEventRemoved;
            _subsManager.Clear();
        }
    }

    private void StartBasicConsume()
    {
        _logger.LogTrace("Starting RabbitMQ basic consume");
        if (_consumerChannel != null)
        {
            if (_consumer == null)
            {
                _consumer = new AsyncEventingBasicConsumer(_consumerChannel);
                _consumer.Received += Consumer_Received;
            }

            _consumerChannel.BasicConsume(queue: _queueName, autoAck: false, consumer: _consumer);
        }
        else
        {
            _logger.LogError("StartBasicConsume can't call on _consumerChannel == null");
        }
    }

    private async Task Consumer_Received(object sender, BasicDeliverEventArgs eventArgs)
    {
        var eventName = eventArgs.RoutingKey;
        var message = Encoding.UTF8.GetString(eventArgs.Body.Span);
        try
        {
            if (message.ToLowerInvariant().Contains("throw-fake-exception"))
            {
                throw new InvalidOperationException($"Fake exception requested: \"{message}\"");
            }
            await ProcessEvent(eventName, message);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "----- ERROR Processing message \"{Message}\"", message);
        }
        _consumerChannel.BasicAck(eventArgs.DeliveryTag, multiple: false);
    }

    private IModel CreateConsumerChannel()
    {
        if (!_persistentConnection.IsConnected)
        {
            _persistentConnection.TryConnect();
        }

        _logger.LogTrace("Creating RabbitMQ consumer channel");
        var channel = _persistentConnection.CreateModel();
        channel.ExchangeDeclare(exchange: BROKER_NAME, type: "direct");
        channel.QueueDeclare(queue: _queueName, durable: true, exclusive: false, autoDelete: false, arguments: null);

        // Create and store the event handler to allow proper cleanup
        _channelCallbackExceptionHandler = OnChannelCallbackException;
        channel.CallbackException += _channelCallbackExceptionHandler;

        return channel;
    }

    private void OnChannelCallbackException(object sender, CallbackExceptionEventArgs ea)
    {
        _logger.LogWarning(ea.Exception, "Recreating RabbitMQ consumer channel");
        try
        {
            if (_consumerChannel != null && _consumerChannel.IsOpen)
            {
                _consumerChannel.Close();
            }
            if (_consumerChannel != null)
            {
                // Unsubscribe from old channel before disposing
                if (_channelCallbackExceptionHandler != null)
                {
                    try
                    {
                        _consumerChannel.CallbackException -= _channelCallbackExceptionHandler;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Error unsubscribing from CallbackException during channel recreation");
                    }
                }
                _consumerChannel.Dispose();
            }
            _consumerChannel = CreateConsumerChannel();
            StartBasicConsume();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during channel recreation");
        }
    }

    private async Task ProcessEvent(string eventName, string message)
    {
        _logger.LogTrace("Processing RabbitMQ event: {EventName}", eventName);
        if (_subsManager.HasSubscriptionsForEvent(eventName))
        {
            await using var scope = _serviceProvider.CreateAsyncScope();
            var subscriptions = _subsManager.GetHandlersForEvent(eventName);
            foreach (var subscription in subscriptions)
            {
                var handler = scope.ServiceProvider.GetService(subscription.HandlerType);
                if (handler == null) continue;

                if (subscription.IsDynamic)
                {
                    if (handler is IDynamicIntegrationEventHandler dynamicHandler)
                    {
                        using dynamic eventData = JsonDocument.Parse(message);
                        await Task.Yield();
                        await dynamicHandler.Handle(eventData);
                    }
                }
                else
                {
                    var eventType = _subsManager.GetEventTypeByName(eventName);
                    if (eventType == null) continue;
                    var integrationEvent = JsonSerializer.Deserialize(message, eventType, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    var concreteType = typeof(IIntegrationEventHandler<>).MakeGenericType(eventType);
                    await Task.Yield();
                    var method = concreteType.GetMethod("Handle");
                    if (method != null)
                    {
                        await (Task)method.Invoke(handler, new object[] { integrationEvent! });
                    }
                }
            }
        }
        else
        {
            _logger.LogWarning("No subscription for RabbitMQ event: {EventName}", eventName);
        }
    }
}
