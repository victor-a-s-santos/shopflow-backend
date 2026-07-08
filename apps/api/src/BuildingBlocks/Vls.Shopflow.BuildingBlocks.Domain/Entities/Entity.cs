using Vls.Shopflow.BuildingBlocks.Domain.Interfaces;

namespace Vls.Shopflow.BuildingBlocks.Domain.Entities;

public abstract class Entity<TId>
{
    public TId Id { get; protected set; } = default!;
    private readonly List<IDomainEvent> _events = new();
    public IReadOnlyCollection<IDomainEvent> DomainEvents => _events.AsReadOnly();

    protected void Raise(IDomainEvent @event) => _events.Add(@event);
    public void ClearEvents() => _events.Clear();
}