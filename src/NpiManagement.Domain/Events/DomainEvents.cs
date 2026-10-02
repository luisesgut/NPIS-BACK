using NpiManagement.Domain.Common;

namespace NpiManagement.Domain.Events;

public sealed record NpiSubmittedEvent(Guid NpiId, DateTimeOffset OccurredAt) : IDomainEvent;

public sealed record EngineeringFeasibilityApprovedEvent(Guid NpiId, DateTimeOffset OccurredAt) : IDomainEvent;

public sealed record DesignFeasibilityApprovedEvent(Guid NpiId, DateTimeOffset OccurredAt) : IDomainEvent;

public sealed record PrintCardFullyApprovedEvent(Guid NpiId, Guid PrintCardVersionId, DateTimeOffset OccurredAt) : IDomainEvent;

public sealed record NpiCycleCompletedEvent(Guid NpiId, DateTimeOffset OccurredAt) : IDomainEvent;

public sealed record NpiRejectedEvent(Guid NpiId, TerminalReason Reason, DateTimeOffset OccurredAt) : IDomainEvent;
