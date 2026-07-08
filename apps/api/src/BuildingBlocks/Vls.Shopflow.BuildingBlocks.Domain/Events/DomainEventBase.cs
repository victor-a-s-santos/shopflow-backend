using Vls.Shopflow.BuildingBlocks.Domain.Interfaces;

namespace Vls.Shopflow.BuildingBlocks.Domain.Events;

public abstract record DomainEventBase : IDomainEvent
{
    public DateTime OccurredOn { get; init; } = DateTime.UtcNow;
}