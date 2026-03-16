namespace Lascodia.Trading.Engine.EventBus.Events;

public record LogAuditTrailIntegrationEvent : IntegrationEvent
{
    public LogAuditTrailIntegrationEvent(string? userId, int? businessId, string? actionType, string? entityType, string? entityFullType, string? entityID, string? previousData, string? newData, string? remark, string? channelCode)
    {
        UserId = userId;
        BusinessId = businessId;
        ActionType = actionType;
        EntityType = entityType;
        EntityFullType = entityFullType;
        EntityID = entityID;
        PreviousData = previousData;
        NewData = newData;
        Remark = remark;
        ChannelCode = channelCode;
    }

    public string? UserId { get; set; }
    public int? BusinessId { get; set; }
    public string? ActionType { get; set; }
    public string? EntityType { get; set; }
    public string? EntityFullType { get; set; }
    public string? EntityID { get; set; }
    public string? PreviousData { get; set; }
    public string? NewData { get; set; }
    public string? Remark { get; set; }
    public string? ChannelCode { get; set; }
    public string? UserData { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
