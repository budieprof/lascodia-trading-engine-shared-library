using System.Diagnostics.CodeAnalysis;
using System.Net.Sockets;
using System.Text.Json;
using Lascodia.Trading.Engine.EventBus;
using Lascodia.Trading.Engine.EventBus.Abstractions;
using Lascodia.Trading.Engine.EventBus.Events;
using Lascodia.Trading.Engine.EventBus.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Retry;

namespace Lascodia.Trading.Engine.EventBusKafka;

[ExcludeFromCodeCoverage]
public class EventBusKafka : IEventBus, IDisposable
{
    const int MAX_CONSUMER_TASKS = 100; // Maximum number of consumer tasks to prevent unbounded growth

    private readonly ILogger<EventBusKafka> _logger;
    private readonly IEventBusSubscriptionsManager _subsManager;
    private readonly ProducerConfig productConfig;
    private readonly ConsumerConfig consumerConfig;
    private readonly IServiceProvider _serviceProvider;
    private readonly int _retryCount;
    private readonly CancellationTokenSource _cancellationTokenSource;
    private readonly Dictionary<string, Task> _consumerTasks;
    private readonly Timer _cleanupTimer;
    private readonly object _consumerTasksLock = new object();
    private bool _disposed = false;

    public EventBusKafka(ILogger<EventBusKafka> logger,
        IServiceProvider serviceProvider, IEventBusSubscriptionsManager subsManager, ProducerConfig producerConfig, ConsumerConfig consumerConfig, int retryCount = 5)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _subsManager = subsManager ?? new InMemoryEventBusSubscriptionsManager();
        this.productConfig = producerConfig;
        this.consumerConfig = consumerConfig;
        _serviceProvider = serviceProvider;
        _retryCount = retryCount;
        _cancellationTokenSource = new CancellationTokenSource();
        _consumerTasks = new Dictionary<string, Task>();

        // Start periodic cleanup timer (runs every 1 minute to prevent task accumulation)
        _cleanupTimer = new Timer(CleanupCompletedTasks, null, 60000, 60000);
    }

    ~EventBusKafka()
    {
        Dispose(false);
    }

    public void Publish(IntegrationEvent @event)
    {
        var policy = RetryPolicy.Handle<Exception>()
            .Or<SocketException>()
            .WaitAndRetry(_retryCount, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)), (ex, time) =>
            {
                _logger.LogWarning(ex, "Could not publish event: {EventId} after {Timeout}s ({ExceptionMessage})", @event.Id, $"{time.TotalSeconds:n1}", ex.Message);
            });

        var eventName = @event.GetType().Name;
        using var producer = new ProducerBuilder<Null, string>(productConfig).Build();
        var body = JsonSerializer.Serialize(@event, @event.GetType(), new JsonSerializerOptions
        {
            WriteIndented = true
        });

        policy.Execute(async () =>
        {
            await producer.ProduceAsync(eventName, new Message<Null, string>
            {
                Value = body
            });
        });
    }

    public void SubscribeDynamic<TH>(string eventName) where TH : IDynamicIntegrationEventHandler
    {
        _logger.LogInformation("Subscribing to dynamic event {EventName} with {EventHandler}", eventName, typeof(TH).GetGenericTypeName());
        _subsManager.AddDynamicSubscription<TH>(eventName);
        CleanupAndAddConsumerTask(eventName);
    }

    private void CleanupCompletedTasks(object? state)
    {{
        lock (_consumerTasksLock)
        {{
            var completedTasks = _consumerTasks.Where(kvp => kvp.Value.IsCompleted || kvp.Value.IsFaulted || kvp.Value.IsCanceled).ToList();

            if (!completedTasks.Any()) return;

            int before = _consumerTasks.Count;
            int faultedCount = 0;
            int canceledCount = 0;
            int completedSuccessfullyCount = 0;

            foreach (var kvp in completedTasks)
            {{
                if (kvp.Value.IsFaulted && kvp.Value.Exception != null)
                {{
                    _logger.LogWarning(kvp.Value.Exception, "Consumer task for event '{{EventName}}' faulted and will be removed", kvp.Key);
                    faultedCount++;
                }}
                else if (kvp.Value.IsCanceled)
                {{
                    canceledCount++;
                }}
                else if (kvp.Value.IsCompletedSuccessfully)
                {{
                    completedSuccessfullyCount++;
                }}

                _consumerTasks.Remove(kvp.Key);
            }}

            int after = _consumerTasks.Count;

            _logger.LogInformation(
                "Cleaned up {{Total}} consumer tasks (Completed: {{Completed}}, Faulted: {{Faulted}}, Canceled: {{Canceled}}). Active tasks: {{ActiveCount}}",
                before - after, completedSuccessfullyCount, faultedCount, canceledCount, after);
        }}
    }}

    private void CleanupAndAddConsumerTask(string eventName)
    {
        lock (_consumerTasksLock)
        {
            // Clean up any completed tasks first
            var completed = _consumerTasks.Where(kvp => kvp.Value.IsCompleted || kvp.Value.IsFaulted || kvp.Value.IsCanceled).ToList();
            foreach (var kvp in completed)
            {
                _consumerTasks.Remove(kvp.Key);
            }

            // If a consumer for this event is already running, do nothing
            if (_consumerTasks.ContainsKey(eventName))
            {
                _logger.LogInformation("Consumer task for event '{EventName}' is already running.", eventName);
                return;
            }

            if (_consumerTasks.Count >= MAX_CONSUMER_TASKS)
            {
                _logger.LogWarning("Maximum consumer task limit ({MaxTasks}) reached. Cannot add new consumer for event {EventName}", MAX_CONSUMER_TASKS, eventName);
                return;
            }

            var task = Task.Run(() => StartBasicConsumeAsync(eventName, _cancellationTokenSource.Token), _cancellationTokenSource.Token);
            _consumerTasks.Add(eventName, task);
        }
    }

    public void Subscribe<T, TH>() where T : IntegrationEvent where TH : IIntegrationEventHandler<T>
    {
        var eventName = _subsManager.GetEventKey<T>();
        _logger.LogInformation("Subscribing to event {EventName} with {EventHandler}", eventName, typeof(TH).GetGenericTypeName());
        _subsManager.AddSubscription<T, TH>();
        CleanupAndAddConsumerTask(eventName);
    }

    public void Subscribe(Type handler)
    {
        if (handler.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IIntegrationEventHandler<>)))
        {
            var eventType = handler.GetInterfaces().First(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IIntegrationEventHandler<>))
                .GetGenericArguments().First();
            var eventName = eventType.Name;
            _logger.LogInformation("Subscribing to event {EventName} with {EventHandler}", eventName, handler.GetGenericTypeName());
            _subsManager.AddSubscription(handler);
            CleanupAndAddConsumerTask(eventName);
        }
    }

    private async Task StartBasicConsumeAsync(string eventName, CancellationToken cancellationToken)
    {
        try
        {
            using var consumer = new ConsumerBuilder<Ignore, string>(consumerConfig).Build();
            consumer.Subscribe(eventName);
            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    var result = consumer.Consume(cancellationToken);
                    await ProcessEvent(eventName, result.Message.Value);
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Consumer for event {EventName} was cancelled", eventName);
                consumer.Close();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in consumer for event {EventName}: {Message}", eventName, ex.Message);
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
            _cleanupTimer?.Dispose();
            _cancellationTokenSource.Cancel();
            try
            {
                Task.WaitAll(_consumerTasks.Values.ToArray(), TimeSpan.FromSeconds(5));
            }
            catch (AggregateException ex)
            {
                _logger.LogWarning(ex, "Some consumer tasks did not complete gracefully during disposal");
            }
            _cancellationTokenSource.Dispose();

            if (_subsManager is IDisposable disposableSubsManager)
            {
                disposableSubsManager.Dispose();
            }
            else
            {
                _subsManager.Clear();
            }
        }
    }

    private async Task ProcessEvent(string eventName, string message)
    {
        _logger.LogTrace("Processing Kafka event: {EventName}", eventName);
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
            _logger.LogWarning("No subscription for Kafka event: {EventName}", eventName);
        }
    }
}