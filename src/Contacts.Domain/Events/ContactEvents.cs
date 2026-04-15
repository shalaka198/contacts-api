using Contacts.Domain.Common;
using Contacts.Domain.ValueObjects;

namespace Contacts.Domain.Events;

public sealed record ContactCreatedEvent(
    ContactId ContactId,
    Email Email) : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTimeOffset OccurredOn { get; } = DateTimeOffset.UtcNow;
}

public sealed record ContactUpdatedEvent(
    ContactId ContactId,
    Email Email) : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTimeOffset OccurredOn { get; } = DateTimeOffset.UtcNow;
}

public sealed record ContactDeletedEvent(
    ContactId ContactId) : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTimeOffset OccurredOn { get; } = DateTimeOffset.UtcNow;
}
