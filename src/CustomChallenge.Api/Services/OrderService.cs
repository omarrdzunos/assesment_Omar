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
        var duplicate = request.Items.GroupBy(i => i.ProductId).FirstOrDefault(g => g.Count() > 1);
        if (duplicate is not null)
        {
            throw new DuplicateOrderItemsException(duplicate.Key);
        }

        // Fixed lock order across all requests (ascending ProductId) so two
        // orders that both touch, say, products 1 and 2 always acquire their
        // row locks in the same order — this rules out a classic deadlock
        // between two concurrent orders locking the same rows in reverse order.
        var orderedItems = request.Items.OrderBy(i => i.ProductId).ToList();

        await using var transaction = await db.Database.BeginTransactionAsync(ct);

        var productIds = orderedItems.Select(i => i.ProductId).ToList();
        var products = await db.Products
            .AsNoTracking()
            .Where(p => productIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, ct);

        var orderItems = new List<OrderItem>();

        foreach (var item in orderedItems)
        {
            // Single atomic, conditional UPDATE: the WHERE clause re-checks
            // IsActive/Stock at write time, on the database itself, so there
            // is no read-then-write gap for a concurrent request to exploit.
            // This bypasses the change tracker entirely, which is exactly why
            // the explicit transaction above is required — without it, this
            // UPDATE and the Order insert below would not be atomic together.
            var affected = await db.Products
                .Where(p => p.Id == item.ProductId && p.IsActive && p.Stock >= item.Quantity)
                .ExecuteUpdateAsync(s => s.SetProperty(p => p.Stock, p => p.Stock - item.Quantity), ct);

            if (affected == 0)
            {
                var product = await db.Products.AsNoTracking().FirstOrDefaultAsync(p => p.Id == item.ProductId, ct);

                if (product is null)
                {
                    throw new ProductNotFoundException(item.ProductId);
                }

                if (!product.IsActive)
                {
                    throw new ProductInactiveException(item.ProductId);
                }

                throw new InsufficientStockException(item.ProductId, item.Quantity, product.Stock);
            }

            orderItems.Add(new OrderItem
            {
                ProductId = item.ProductId,
                Quantity = item.Quantity,
                UnitPrice = products[item.ProductId].Price
            });
        }

        var order = new Order
        {
            CreatedAt = DateTime.UtcNow,
            Status = OrderStatus.Pending,
            Items = orderItems
        };

        db.Orders.Add(order);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

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
        return await db.Orders
            .Where(o => o.Id == id)
            .Select(o => new OrderResponse(
                o.Id,
                o.CreatedAt,
                o.Status,
                o.Items.Select(i => new OrderItemResponse(i.ProductId, i.Product!.Name, i.Quantity, i.UnitPrice)).ToList(),
                o.Items.Sum(i => i.Quantity * i.UnitPrice)))
            .FirstOrDefaultAsync(ct)
            ?? throw new OrderNotFoundException(id);
    }

    public async Task CancelOrderAsync(int id, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);

        // Atomic, conditional status transition: if two cancel requests for
        // the same order race, only one of them matches "Status = Pending"
        // and flips it — the other affects zero rows and never restores stock.
        var affected = await db.Orders
            .Where(o => o.Id == id && o.Status == OrderStatus.Pending)
            .ExecuteUpdateAsync(s => s.SetProperty(o => o.Status, OrderStatus.Cancelled), ct);

        if (affected == 0)
        {
            var exists = await db.Orders.AsNoTracking().AnyAsync(o => o.Id == id, ct);
            if (!exists)
            {
                throw new OrderNotFoundException(id);
            }

            throw new OrderNotPendingException(id);
        }

        var items = await db.OrderItems
            .AsNoTracking()
            .Where(i => i.OrderId == id)
            .OrderBy(i => i.ProductId)
            .Select(i => new { i.ProductId, i.Quantity })
            .ToListAsync(ct);

        foreach (var item in items)
        {
            await db.Products
                .Where(p => p.Id == item.ProductId)
                .ExecuteUpdateAsync(s => s.SetProperty(p => p.Stock, p => p.Stock + item.Quantity), ct);
        }

        await transaction.CommitAsync(ct);
    }

    private static OrderResponse MapToResponse(Order order, IReadOnlyDictionary<int, Product> products)
    {
        var items = order.Items
            .Select(i => new OrderItemResponse(i.ProductId, products[i.ProductId].Name, i.Quantity, i.UnitPrice))
            .ToList();

        return new OrderResponse(order.Id, order.CreatedAt, order.Status, items, items.Sum(i => i.Quantity * i.UnitPrice));
    }
}
