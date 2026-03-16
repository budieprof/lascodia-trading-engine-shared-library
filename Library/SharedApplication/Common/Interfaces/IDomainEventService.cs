using Lascodia.Trading.Engine.SharedDomain.Common;

namespace Lascodia.Trading.Engine.SharedApplication.Common.Interfaces;

public interface IDomainEventService
{
    Task Publish(DomainEvent domainEvent);
}
