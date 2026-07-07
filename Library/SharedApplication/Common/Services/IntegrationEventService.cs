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

            // Best-effort demotion to PublishedFailed so the outbox retry worker picks the
            // row up on its next cycle. If this status write itself fails (e.g. the DB just
            // went away), the row simply stays NotPublished/InProgress — both of which the
            // retry worker also sweeps once they age past its stuck threshold. Either way
            // the committed outbox row is the durable source of truth; we never rethrow
            // here because the caller's domain transaction has already committed.
            try
            {
                await _eventLogService.MarkEventAsFailedAsync(evt.Id);
            }
            catch (Exception markEx)
            {
                _logger.LogError(markEx, "ERROR marking integration event as failed: {IntegrationEventId} from {AppName}; row remains in its pre-publish state for the outbox retry sweep.", evt.Id, _appName);
            }
        }
    }

    private async Task SaveEventAndContextChangesAsync(IDbContext _context, IntegrationEvent evt)
    {
        _logger.LogInformation("----- IntegrationEventService - Saving changes and integrationEvent: {IntegrationEventId}", evt.Id);

        // Transactional outbox (E-3 fix):
        //
        // The transactional/retryable unit below contains ONLY the domain SaveChanges and
        // the NotPublished IntegrationEventLog row. The broker publish is deliberately
        // OUTSIDE both the transaction and the retrying execution strategy, because:
        //   1. Publishing inside the uncommitted transaction let consumers observe events
        //      for domain rows that could still roll back (commit-after-publish inversion).
        //   2. EnableRetryOnFailure replays the whole delegate on transient failures, which
        //      previously produced 1-3 duplicate broker publishes with no committed row.
        //
        // Failure semantics after this change:
        //   - Transaction fails  -> nothing published, nothing committed. Caller sees the
        //     exception; no outbox row exists, so no reconcile path is needed.
        //   - Transaction commits, publish fails -> the row stays NotPublished (or is
        //     demoted to PublishedFailed) and the engine's IntegrationEventRetryWorker
        //     sweeps and re-publishes it. At-least-once delivery; handlers must stay
        //     idempotent.
        //
        //Use of an EF Core resiliency strategy when using multiple DbContexts within an explicit BeginTransaction():
        //See: https://docs.microsoft.com/en-us/ef/core/miscellaneous/connection-resiliency
        await ResilientTransaction.New(_context.GetDbContext()).ExecuteAsync(async () =>
        {
            // Achieving atomicity between original request database operation and the IntegrationEventLog thanks to a local transaction
            await _context.SaveChangesAsync();
            await _eventLogService.SaveEventAsync(evt, _context.GetDbContext().Database.CurrentTransaction, _context.GetDbContext().Database.GetDbConnection()) ;
        });

        // Publish strictly AFTER a successful commit. PublishThroughEventBusAsync never
        // throws: publish failures leave the committed outbox row for the retry sweep.
        await PublishThroughEventBusAsync(evt);
    }

    public async Task SaveAndPublish(IDbContext _context, IntegrationEvent evt)
    {
        await SaveEventAndContextChangesAsync(_context,evt);
    }
}
