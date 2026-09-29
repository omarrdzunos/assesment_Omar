using CustomChallenge.Api.Data;
using CustomChallenge.Api.Domain;
using CustomChallenge.Api.Dtos;
using CustomChallenge.Api.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace CustomChallenge.Api.Services;

public class OrderService(AppDbContext db) : IOrderService
{
    public async Task<OrderResponse> CreateOrderAsync(CreateOrderRequest request, CancellationToken ct)
    {
        // Collapse duplicate ProductId entries so "buy 2 laptops" split across
        // two lines is validated/decremented as a single quantity of 2.
        var requestedQuantities = request.Items
            .GroupBy(i => i.ProductId)
            .ToDictionary(g => g.Key, g => g.Sum(i => i.Quantity));

        var productIds = requestedQuantities.Keys.ToList();
        var products = await db.Products
            .Where(p => productIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, ct);

        foreach (var (productId, quantity) in requestedQuantities)
        {
            if (!products.TryGetValue(productId, out var product))
            {
                throw new ProductNotFoundException(productId);
            }

            if (!product.IsActive)
            {
                throw new ProductInactiveException(productId);
            }

            if (product.Stock < quantity)
            {
                throw new InsufficientStockException(productId, quantity, product.Stock);
            }
        }

        var order = new Order
        {
            CreatedAt = DateTime.UtcNow,
            Status = OrderStatus.Pending
        };

        foreach (var (productId, quantity) in requestedQuantities)
        {
            var product = products[productId];
            product.Stock -= quantity;

            order.Items.Add(new OrderItem
            {
                ProductId = product.Id,
                Quantity = quantity,
                UnitPrice = product.Price
            });
        }

        db.Orders.Add(order);

        await SaveWithConcurrencyHandlingAsync(ct);

        return MapToResponse(order, products);
    }

    public async Task<IReadOnlyList<OrderSummaryResponse>> GetOrdersAsync(CancellationToken ct)
    {
        return await db.Orders
            .OrderByDescending(o => o.CreatedAt)
            .Select(o => new OrderSummaryResponse(o.Id, o.CreatedAt, o.Status))
            .ToListAsync(ct);
    }

    public async Task<OrderResponse> GetOrderByIdAsync(int id, CancellationToken ct)
    {
        var order = await db.Orders
            .Include(o => o.Items)
            .ThenInclude(i => i.Product)
            .FirstOrDefaultAsync(o => o.Id == id, ct)
            ?? throw new OrderNotFoundException(id);

        return MapToResponse(order);
    }

    public async Task CancelOrderAsync(int id, CancellationToken ct)
    {
        var order = await db.Orders
            .Include(o => o.Items)
            .ThenInclude(i => i.Product)
            .FirstOrDefaultAsync(o => o.Id == id, ct)
            ?? throw new OrderNotFoundException(id);

        if (order.Status != OrderStatus.Pending)
        {
            throw new OrderNotPendingException(id);
        }

        foreach (var item in order.Items)
        {
            item.Product!.Stock += item.Quantity;
        }

        order.Status = OrderStatus.Cancelled;

        await SaveWithConcurrencyHandlingAsync(ct);
    }

    private async Task SaveWithConcurrencyHandlingAsync(CancellationToken ct)
    {
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            var conflictingProductId = ex.Entries
                .Select(e => e.Entity)
                .OfType<Product>()
                .Select(p => p.Id)
                .FirstOrDefault();

            throw new StockConflictException(conflictingProductId);
        }
    }

    private static OrderResponse MapToResponse(Order order, IReadOnlyDictionary<int, Product> products)
    {
        var items = order.Items
            .Select(i => new OrderItemResponse(i.ProductId, products[i.ProductId].Name, i.Quantity, i.UnitPrice))
            .ToList();

        return new OrderResponse(order.Id, order.CreatedAt, order.Status, items, items.Sum(i => i.Quantity * i.UnitPrice));
    }

    private static OrderResponse MapToResponse(Order order)
    {
        var items = order.Items
            .Select(i => new OrderItemResponse(i.ProductId, i.Product?.Name ?? string.Empty, i.Quantity, i.UnitPrice))
            .ToList();

        return new OrderResponse(order.Id, order.CreatedAt, order.Status, items, items.Sum(i => i.Quantity * i.UnitPrice));
    }
}
