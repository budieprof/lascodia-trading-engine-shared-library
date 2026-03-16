using Lascodia.Trading.Engine.EventBus.Events;

namespace Lascodia.Trading.Engine.SharedApplication.Common.Interfaces;

public interface IIntegrationEventService
{
    Task SaveAndPublish(IDbContext _context, IntegrationEvent evt);
}
