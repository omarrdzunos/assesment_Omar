using CustomChallenge.Api.Dtos;

namespace CustomChallenge.Api.Services;

public interface IProductService
{
    Task<IReadOnlyList<ProductResponse>> GetAllAsync(CancellationToken ct);
}
