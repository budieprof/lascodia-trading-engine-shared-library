using MediatR;
using Lascodia.Trading.Engine.SharedDomain.Common;
using System.Diagnostics.CodeAnalysis;

namespace Lascodia.Trading.Engine.SharedApplication.Common.Models;

[ExcludeFromCodeCoverage]
public class DomainEventNotification<TDomainEvent> : INotification where TDomainEvent : DomainEvent
{
    public DomainEventNotification(TDomainEvent domainEvent)
    {
        DomainEvent = domainEvent;
    }

    public TDomainEvent DomainEvent { get; }
}
