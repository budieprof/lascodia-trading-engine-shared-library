using Lascodia.Trading.Engine.EventBus.Abstractions;
using Lascodia.Trading.Engine.EventBus.Events;
using Lascodia.Trading.Engine.IntegrationEventLogEF.Services;
using Lascodia.Trading.Engine.IntegrationEventLogEF.Utilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Lascodia.Trading.Engine.SharedApplication.Common.Interfaces;
using System.Diagnostics.CodeAnalysis;

namespace Lascodia.Trading.Engine.SharedApplication.Common.Services;

[ExcludeFromCodeCoverage]
public class IntegrationEventService : IIntegrationEventService
{
    private readonly IEventBus _eventBus;
    private readonly IIntegrationEventLogService _eventLogService;
    private readonly ILogger<IntegrationEventService> _logger;
    private string? _appName;

    public IntegrationEventService(
        ILogger<IntegrationEventService> logger,
        IEventBus eventBus,
        IIntegrationEventLogService integrationEventLogService, string appName)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _eventLogService = integrationEventLogService ?? throw new ArgumentNullException(nameof(integrationEventLogService));
        _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
        this._appName = appName;
    }

    private async Task PublishThroughEventBusAsync(IntegrationEvent evt)
    {
        try
        {
            _logger.LogInformation("----- Publishing integration event: {IntegrationEventId_published} from {AppName} - ({@IntegrationEvent})", evt.Id, _appName, evt);


            await _eventLogService.MarkEventAsInProgressAsync(evt.Id);
            _eventBus.Publish(evt);
            await _eventLogService.MarkEventAsPublishedAsync(evt.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ERROR Publishing integration event: {IntegrationEventId} from {AppName} - ({@IntegrationEvent})", evt.Id, _appName, evt);
            await _eventLogService.MarkEventAsFailedAsync(evt.Id);
        }
    }

    private async Task SaveEventAndContextChangesAsync(IDbContext _context, IntegrationEvent evt)
    {
        _logger.LogInformation("----- IntegrationEventService - Saving changes and integrationEvent: {IntegrationEventId}", evt.Id);

        //Use of an EF Core resiliency strategy when using multiple DbContexts within an explicit BeginTransaction():
        //See: https://docs.microsoft.com/en-us/ef/core/miscellaneous/connection-resiliency            
        await ResilientTransaction.New(_context.GetDbContext()).ExecuteAsync(async () =>
        {
            // Achieving atomicity between original request database operation and the IntegrationEventLog thanks to a local transaction
            await _context.SaveChangesAsync();
            await _eventLogService.SaveEventAsync(evt, _context.GetDbContext().Database.CurrentTransaction, _context.GetDbContext().Database.GetDbConnection()) ;
            await PublishThroughEventBusAsync(evt);
        });
    }

    public async Task SaveAndPublish(IDbContext _context, IntegrationEvent evt)
    {
        await SaveEventAndContextChangesAsync(_context,evt);
    }
}
