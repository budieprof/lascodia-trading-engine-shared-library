using System.Text.Json;

namespace Lascodia.Trading.Engine.EventBus.Abstractions;

/// <summary>
/// Carries the tenant (organisation) of the work that publishes an integration event to the work that handles it.
/// Optional: a host that registers none publishes and handles events without a tenant, as before.
/// </summary>
public interface IIntegrationEventTenancy
{
    /// <summary>The tenant the publishing unit of work acts for; null outside one (a platform event).</summary>
    long? CurrentTenantId { get; }

    /// <summary>
    /// Opens the scope a handler runs in until disposed: the event's tenant, or — when the event has none — the
    /// host's platform scope.
    /// </summary>
    IDisposable BeginHandlerScope(long? tenantId, string eventName);
}

/// <summary>Helpers the buses share.</summary>
public static class IntegrationEventTenancy
{
    /// <summary>The <c>TenantId</c> of a serialized event (any casing); null when absent, null or unreadable.</summary>
    public static long? ReadTenantId(string message)
    {
        try
        {
            using var document = JsonDocument.Parse(message);
            if (document.RootElement.ValueKind != JsonValueKind.Object) return null;
            foreach (var property in document.RootElement.EnumerateObject())
            {
                if (!property.Name.Equals(nameof(IntegrationEvent.TenantId), StringComparison.OrdinalIgnoreCase)) continue;
                return property.Value.ValueKind == JsonValueKind.Number && property.Value.TryGetInt64(out var id) ? id : null;
            }
            return null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>The handler scope for <paramref name="tenantId"/>, or null when the host registered no tenancy.</summary>
    public static IDisposable? BeginHandlerScope(IServiceProvider services, long? tenantId, string eventName)
        => (services.GetService(typeof(IIntegrationEventTenancy)) as IIntegrationEventTenancy)?.BeginHandlerScope(tenantId, eventName);
}
