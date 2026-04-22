namespace Lascodia.Trading.Engine.IntegrationEventLogEF;

public abstract class IntegrationEventLogContext<T> : DbContext where T : DbContext
{
    public IntegrationEventLogContext(DbContextOptions<T> options) : base(options)
    {
    }

    public DbSet<IntegrationEventLogEntry> IntegrationEventLogs { get; set; }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        builder.Entity<IntegrationEventLogEntry>(ConfigureIntegrationEventLogEntry);
    }

    private void ConfigureIntegrationEventLogEntry(EntityTypeBuilder<IntegrationEventLogEntry> builder)
    {
        builder.ToTable("IntegrationEventLog");

        builder.HasKey(e => e.EventId);

        builder.Property(e => e.EventId)
            .IsRequired();

        builder.Property(e => e.Content)
            .IsRequired();

        builder.Property(e => e.CreationTime)
            .IsRequired();

        builder.Property(e => e.State)
            .IsRequired();

        builder.Property(e => e.TimesSent)
            .IsRequired();

        builder.Property(e => e.EventTypeName)
            .IsRequired();

        // Cleanup query path: DELETE rows WHERE State = <PublishedNotAcknowledged>
        // AND CreationTime < <cutoff> ORDER BY CreationTime LIMIT N.
        // Without this index the purge scans the full IntegrationEventLog table and
        // was observed at ~5.9 s on a live instance; (State, CreationTime) turns it
        // into an indexed range scan.
        builder.HasIndex(e => new { e.State, e.CreationTime })
            .HasDatabaseName("IX_IntegrationEventLog_State_CreationTime");

    }
}
