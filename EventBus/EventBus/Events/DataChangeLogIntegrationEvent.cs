using System;

namespace Lascodia.Trading.Engine.EventBus.Events;

public record DataChangeLogIntegrationEvent : IntegrationEvent
	{
        public DataChangeLogIntegrationEvent(string entityId, string changeMaker, string entity, string action, string data, string oldData, DateTime changedAt)
        {
            EntityId = entityId;
            ChangeMaker = changeMaker;
            Entity = entity;
            Action = action;
            Data = data;
            OldData = oldData;
            ChangedAt = changedAt;
        }

        public string EntityId { get; private init; }
        public string ChangeMaker { get; private init; }
        public string Entity { get; private init; }
        public string Action { get; private init; }
        public string Data { get; private init; }
        public string OldData { get; private init; }
        public DateTime ChangedAt { get; private init; }
    }