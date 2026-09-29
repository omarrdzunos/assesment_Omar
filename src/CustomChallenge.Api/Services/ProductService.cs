using CustomChallenge.Api.Data;
using CustomChallenge.Api.Dtos;
using Microsoft.EntityFrameworkCore;

namespace CustomChallenge.Api.Services;

public class ProductService(AppDbContext db) : IProductService
{
    public async Task<IReadOnlyList<ProductResponse>> GetAllAsync(CancellationToken ct)
    {
        return await db.Products
            .OrderBy(p => p.Id)
            .Select(p => new ProductResponse(p.Id, p.Name, p.Price, p.Stock, p.IsActive))
            .ToListAsync(ct);
    }
}
