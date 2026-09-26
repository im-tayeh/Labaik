using Labaik.Domain.Common;
using MediatR;

namespace Labaik.Application.Common.Events;

// Wraps a pure domain event so MediatR can publish it.
public sealed record DomainEventNotification<TDomainEvent>(TDomainEvent DomainEvent)
    : INotification where TDomainEvent : IDomainEvent;