using CustomChallenge.Api.Domain;

namespace CustomChallenge.Api.Dtos;

public record OrderItemResponse(int ProductId, string ProductName, int Quantity, decimal UnitPrice);

public record OrderResponse(
    int Id,
    DateTime CreatedAt,
    OrderStatus Status,
    IReadOnlyList<OrderItemResponse> Items,
    decimal Total);

public record OrderSummaryResponse(int Id, DateTime CreatedAt, OrderStatus Status);
