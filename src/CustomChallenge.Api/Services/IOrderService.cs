using CustomChallenge.Api.Dtos;

namespace CustomChallenge.Api.Services;

public interface IOrderService
{
    Task<OrderResponse> CreateOrderAsync(CreateOrderRequest request, CancellationToken ct);
    Task<IReadOnlyList<OrderSummaryResponse>> GetOrdersAsync(CancellationToken ct);
    Task<OrderResponse> GetOrderByIdAsync(int id, CancellationToken ct);
    Task CancelOrderAsync(int id, CancellationToken ct);
}
