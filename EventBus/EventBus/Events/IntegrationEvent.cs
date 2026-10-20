using System.Diagnostics.CodeAnalysis;

namespace Lascodia.Trading.Engine.EventBus.Events;
[ExcludeFromCodeCoverage]
public record IntegrationEvent
{        
    public IntegrationEvent()
    {
        Id = Guid.NewGuid();
        CreationDate = DateTime.UtcNow;
    }

    [JsonConstructor]
    public IntegrationEvent(Guid id, DateTime createDate)
    {
        Id = id;
        CreationDate = createDate;
    }

    [JsonInclude]
    public Guid Id { get; private init; }

    [JsonInclude]
    public DateTime CreationDate { get; private init; }

    /// <summary>
    /// The tenant (organisation) the event belongs to; null = a platform event. The publisher stamps it from the
    /// publishing work's tenant when it is not set, and handlers run inside that tenant's scope. Messages written
    /// before it existed carry none and read as null.
    /// </summary>
    [JsonInclude]
    public long? TenantId { get; set; }
}
