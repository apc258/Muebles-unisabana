namespace Shared.Contracts.Events;

public record OrderCreated(Guid OrderId, Guid CustomerId, decimal TotalAmount);
