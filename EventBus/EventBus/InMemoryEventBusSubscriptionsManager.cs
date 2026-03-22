namespace Lascodia.Trading.Engine.EventBus;

public partial class InMemoryEventBusSubscriptionsManager : IEventBusSubscriptionsManager, IDisposable
{
    private const int MAX_SUBSCRIPTIONS = 1000; // Maximum total subscriptions allowed
    private const int MAX_HANDLERS_PER_EVENT = 50; // Maximum handlers per event type
    private const int SUBSCRIPTION_TIMEOUT_HOURS = 24; // Hours before an unused subscription is considered stale

    private readonly Dictionary<string, List<SubscriptionInfo>> _handlers;
    private readonly List<Type> _eventTypes;
    // Track last access time for each event to enable cleanup of stale subscriptions
    private readonly Dictionary<string, DateTime> _lastAccessTime;
    private bool _disposed = false;

    public event EventHandler<string>? OnEventRemoved;

    public InMemoryEventBusSubscriptionsManager()
    {
        _handlers = new Dictionary<string, List<SubscriptionInfo>>();
        _eventTypes = new List<Type>();
        _lastAccessTime = new Dictionary<string, DateTime>();
    }

    public bool IsEmpty => _handlers is { Count: 0 };
    public void Clear()
    {
        _handlers.Clear();
        _lastAccessTime.Clear();
    }

    /// <summary>
    /// Gets the total number of subscriptions across all events
    /// </summary>
    public int TotalSubscriptionCount => _handlers.Values.Sum(list => list.Count);

    public void AddDynamicSubscription<TH>(string eventName)
        where TH : IDynamicIntegrationEventHandler
    {
        DoAddSubscription(typeof(TH), eventName, isDynamic: true);
    }

    public void AddSubscription<T, TH>()
        where T : IntegrationEvent
        where TH : IIntegrationEventHandler<T>
    {
        var eventName = GetEventKey<T>();

        DoAddSubscription(typeof(TH), eventName, isDynamic: false);

        if (!_eventTypes.Contains(typeof(T)))
        {
            _eventTypes.Add(typeof(T));
        }
    }

    public void AddSubscription(Type handler)
    {
        if(handler.GetInterfaces().AsEnumerable().Any(i =>
                    i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IIntegrationEventHandler<>)))
        {
            Type[] args = handler.GetInterfaces().First(i =>
                i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IIntegrationEventHandler<>)).GenericTypeArguments;
            var evt = args[0];
            var eventName = evt.Name;

            DoAddSubscription(handler, eventName, isDynamic: false);

            if (!_eventTypes.Contains(evt))
            {
                _eventTypes.Add(evt);
            }
        }
        
    }

    private void DoAddSubscription(Type handlerType, string eventName, bool isDynamic)
    {
        // Check total subscription limit
        if (TotalSubscriptionCount >= MAX_SUBSCRIPTIONS)
        {
            throw new InvalidOperationException(
                $"Maximum subscription limit of {MAX_SUBSCRIPTIONS} reached. Cannot add more subscriptions.");
        }

        if (!HasSubscriptionsForEvent(eventName))
        {
            _handlers.Add(eventName, new List<SubscriptionInfo>());
            // Track when this event was first subscribed to
            _lastAccessTime[eventName] = DateTime.UtcNow;
        }

        // Check per-event handler limit
        if (_handlers[eventName].Count >= MAX_HANDLERS_PER_EVENT)
        {
            throw new InvalidOperationException(
                $"Maximum handlers per event limit of {MAX_HANDLERS_PER_EVENT} reached for event '{eventName}'.");
        }

        if (_handlers[eventName].Exists(s => s.HandlerType == handlerType))
        {
            throw new ArgumentException(
                $"Handler Type {handlerType.Name} already registered for '{eventName}'", nameof(handlerType));
        }

        // Warn when approaching limits (at 80%)
        if (TotalSubscriptionCount >= MAX_SUBSCRIPTIONS * 0.8)
        {
            Console.WriteLine($"WARNING: Subscription count ({TotalSubscriptionCount}) is approaching the limit of {MAX_SUBSCRIPTIONS}");
        }

        if (_handlers[eventName].Count >= MAX_HANDLERS_PER_EVENT * 0.8)
        {
            Console.WriteLine($"WARNING: Handler count for event '{eventName}' ({_handlers[eventName].Count}) is approaching the limit of {MAX_HANDLERS_PER_EVENT}");
        }

        if (isDynamic)
        {
            _handlers[eventName].Add(SubscriptionInfo.Dynamic(handlerType));
        }
        else
        {
            _handlers[eventName].Add(SubscriptionInfo.Typed(handlerType));
        }
    }


    public void RemoveDynamicSubscription<TH>(string eventName)
        where TH : IDynamicIntegrationEventHandler
    {
        var handlerToRemove = FindDynamicSubscriptionToRemove<TH>(eventName);
        DoRemoveHandler(eventName, handlerToRemove);
    }


    public void RemoveSubscription<T, TH>()
        where TH : IIntegrationEventHandler<T>
        where T : IntegrationEvent
    {
        var handlerToRemove = FindSubscriptionToRemove<T, TH>();
        var eventName = GetEventKey<T>();
        DoRemoveHandler(eventName, handlerToRemove);
    }

    public void RemoveSubscription(Type handlerType)
    {
        if (handlerType.GetInterfaces().AsEnumerable().Any(i =>
                    i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IIntegrationEventHandler<>)))
        {
            Type[] args = handlerType.GetInterfaces()[0].GenericTypeArguments;
            var eventType = args[0];
            var eventName = eventType.Name;

            var subToRemove = DoFindSubscriptionToRemove(eventName, handlerType);
            DoRemoveHandler(eventName, subToRemove);
        }
    }


    private void DoRemoveHandler(string eventName, SubscriptionInfo? subsToRemove)
    {
        if (subsToRemove != null)
        {
            _handlers[eventName].Remove(subsToRemove);
            if (!_handlers[eventName].Any())
            {
                _handlers.Remove(eventName);
                _lastAccessTime.Remove(eventName);
                var eventType = _eventTypes.SingleOrDefault(e => e.Name == eventName);
                if (eventType != null)
                {
                    _eventTypes.Remove(eventType);
                }
                RaiseOnEventRemoved(eventName);
            }
        }
    }

    public IEnumerable<SubscriptionInfo> GetHandlersForEvent<T>() where T : IntegrationEvent
    {
        var key = GetEventKey<T>();
        return GetHandlersForEvent(key);
    }
    public IEnumerable<SubscriptionInfo> GetHandlersForEvent(string eventName)
    {
        // Update last access time when handlers are retrieved
        if (_lastAccessTime.ContainsKey(eventName))
        {
            _lastAccessTime[eventName] = DateTime.UtcNow;
        }
        return _handlers[eventName];
    }

    private void RaiseOnEventRemoved(string eventName)
    {
        var handler = OnEventRemoved;
        handler?.Invoke(this, eventName);
    }


    private SubscriptionInfo? FindDynamicSubscriptionToRemove<TH>(string eventName)
        where TH : IDynamicIntegrationEventHandler
    {
        return DoFindSubscriptionToRemove(eventName, typeof(TH));
    }


    private SubscriptionInfo? FindSubscriptionToRemove<T, TH>()
            where T : IntegrationEvent
            where TH : IIntegrationEventHandler<T>
    {
        var eventName = GetEventKey<T>();
        return DoFindSubscriptionToRemove(eventName, typeof(TH));
    }

    private SubscriptionInfo? DoFindSubscriptionToRemove(string eventName, Type handlerType)
    {
        if (!HasSubscriptionsForEvent(eventName))
        {
            return null;
        }
        return _handlers[eventName].Find(s => s.HandlerType == handlerType);
    }

    public bool HasSubscriptionsForEvent<T>() where T : IntegrationEvent
    {
        var key = GetEventKey<T>();
        return HasSubscriptionsForEvent(key);
    }
    public bool HasSubscriptionsForEvent(string eventName) => _handlers.ContainsKey(eventName);

    public Type? GetEventTypeByName(string eventName) => _eventTypes.Find(t => t.Name == eventName);

    public string GetEventKey<T>()
    {
        return typeof(T).Name;
    }

    /// <summary>
    /// Removes subscriptions that haven't been accessed within the specified timeout period.
    /// This helps prevent memory leaks in long-running applications by cleaning up stale subscriptions.
    /// </summary>
    /// <param name="timeoutHours">Number of hours of inactivity before a subscription is considered stale. Defaults to SUBSCRIPTION_TIMEOUT_HOURS.</param>
    /// <returns>Number of stale subscriptions removed</returns>
    public int RemoveStaleSubscriptions(int? timeoutHours = null)
    {
        var timeout = timeoutHours ?? SUBSCRIPTION_TIMEOUT_HOURS;
        var cutoffTime = DateTime.UtcNow.AddHours(-timeout);
        var staleEvents = _lastAccessTime
            .Where(kvp => kvp.Value < cutoffTime)
            .Select(kvp => kvp.Key)
            .ToList();

        int removedCount = 0;
        foreach (var eventName in staleEvents)
        {
            if (_handlers.ContainsKey(eventName))
            {
                removedCount += _handlers[eventName].Count;
                _handlers.Remove(eventName);
                _lastAccessTime.Remove(eventName);

                // Clean up associated event type
                var eventType = _eventTypes.SingleOrDefault(e => e.Name == eventName);
                if (eventType != null)
                {
                    _eventTypes.Remove(eventType);
                }

                RaiseOnEventRemoved(eventName);
            }
        }

        if (removedCount > 0)
        {
            Console.WriteLine($"INFO: Removed {removedCount} stale subscription(s) across {staleEvents.Count} event(s) that were inactive for more than {timeout} hours.");
        }

        return removedCount;
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed) return;

        if (disposing)
        {
            // Clear all event handlers to prevent memory leaks
            if (OnEventRemoved != null)
            {
                foreach (var handler in OnEventRemoved.GetInvocationList())
                {
                    OnEventRemoved -= (EventHandler<string>)handler;
                }
            }

            // Clear collections
            _handlers.Clear();
            _eventTypes.Clear();
            _lastAccessTime.Clear();
        }

        _disposed = true;
    }
}
