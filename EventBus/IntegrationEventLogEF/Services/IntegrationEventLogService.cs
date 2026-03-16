namespace Lascodia.Trading.Engine.IntegrationEventLogEF.Services;

public class IntegrationEventLogService<T> : IIntegrationEventLogService, IDisposable where T : DbContext
{
    private readonly IntegrationEventLogContext<T> _integrationEventLogContext;
    // Changed from static to instance-level to prevent holding Type references indefinitely
    // Using Lazy<T> for thread-safe initialization
    private readonly Lazy<List<Type>> _eventTypes;
    private volatile bool _disposedValue;

    public IntegrationEventLogService(IntegrationEventLogContext<T> db)
    {
        _integrationEventLogContext = db;
        _eventTypes = new Lazy<List<Type>>(() =>
        {
            try
            {
                return Assembly.Load(Assembly.GetEntryAssembly().FullName)
                    .GetTypes()
                    .Where(t => t.Name.EndsWith(nameof(IntegrationEvent)))
                    .ToList();
            }
            catch
            {
                // Fallback to empty list if assembly load fails
                return new List<Type>();
            }
        });
    }

    public async Task<IEnumerable<IntegrationEventLogEntry>> RetrieveEventLogsPendingToPublishAsync(Guid transactionId)
    {
        var tid = transactionId.ToString();

        var result = await _integrationEventLogContext.IntegrationEventLogs
            .Where(e => e.TransactionId == tid && e.State == EventStateEnum.NotPublished).ToListAsync();

        if (result != null && result.Any())
        {
            return result.OrderBy(o => o.CreationTime)
                .Select(e => e.DeserializeJsonContent(_eventTypes.Value.Find(t => t.Name == e.EventTypeShortName)));
        }

        return new List<IntegrationEventLogEntry>();
    }

    public Task SaveEventAsync(IntegrationEvent @event, IDbContextTransaction? transaction, DbConnection dbConnection)
    {
        if (transaction == null) throw new ArgumentNullException(nameof(transaction));

        var eventLogEntry = new IntegrationEventLogEntry(@event, transaction.TransactionId);
        _integrationEventLogContext.Database.SetDbConnection(dbConnection);
        _integrationEventLogContext.Database.UseTransaction(transaction.GetDbTransaction());
        _integrationEventLogContext.IntegrationEventLogs.Add(eventLogEntry);

        return _integrationEventLogContext.SaveChangesAsync();
    }

    public Task MarkEventAsPublishedAsync(Guid eventId)
    {
        return UpdateEventStatus(eventId, EventStateEnum.Published);
    }

    public Task MarkEventAsInProgressAsync(Guid eventId)
    {
        return UpdateEventStatus(eventId, EventStateEnum.InProgress);
    }

    public Task MarkEventAsFailedAsync(Guid eventId)
    {
        return UpdateEventStatus(eventId, EventStateEnum.PublishedFailed);
    }

    private async Task UpdateEventStatus(Guid eventId, EventStateEnum status)
    {
        var eventLogEntry = await _integrationEventLogContext.IntegrationEventLogs.FirstOrDefaultAsync(ie => ie.EventId == eventId);
        if(eventLogEntry != null)
        {
            eventLogEntry.State = status;

            if (status == EventStateEnum.InProgress)
                eventLogEntry.TimesSent++;

            _integrationEventLogContext.IntegrationEventLogs.Update(eventLogEntry);

            await _integrationEventLogContext.SaveChangesAsync();
        }
        
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposedValue)
        {
            if (disposing)
            {
                _integrationEventLogContext?.Dispose();
            }


            _disposedValue = true;
        }
    }

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}
