using System;

namespace Lascodia.Trading.Engine.EventBus.Events;

public record TestIntegrationEvent : IntegrationEvent
{
    public string Message { get; init; }

    public TestIntegrationEvent(string message)
    {
        Message = message;
    }
}
