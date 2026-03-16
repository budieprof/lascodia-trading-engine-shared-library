namespace Lascodia.Trading.Engine.SharedDomain.Common;

public abstract class Entity<T>
{
    public T Id { get; set; }
    public Guid OutboxId { get; set; } = Guid.NewGuid();
}
