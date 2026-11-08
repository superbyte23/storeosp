using StoreOps.Domain.Entities;

namespace StoreOps.Application.Contracts.Orders;

public sealed record OrderItemRequest(Guid ProductId, int Quantity);
public sealed record CreateOrderRequest(IReadOnlyList<OrderItemRequest> Items);
public sealed record OrderStatusRequest(string Status);
public sealed record OrderItemDto(Guid ProductId, string ProductName, int Quantity, decimal UnitPrice);
public sealed record OrderDto(
    Guid Id,
    OrderStatus Status,
    DateTime CreatedAtUtc,
    DateTime? FulfilledAtUtc,
    decimal Total,
    IReadOnlyList<OrderItemDto> Items);
