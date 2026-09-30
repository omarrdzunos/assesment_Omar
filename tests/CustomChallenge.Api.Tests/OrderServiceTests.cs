using CustomChallenge.Api.Domain;
using CustomChallenge.Api.Dtos;
using CustomChallenge.Api.Exceptions;
using CustomChallenge.Api.Services;
using Shouldly;

namespace CustomChallenge.Api.Tests;

public class OrderServiceTests : IDisposable
{
    private readonly SqliteTestDatabase _db;
    private readonly OrderService _sut;

    public OrderServiceTests()
    {
        _db = new SqliteTestDatabase();
        _sut = new OrderService(_db.Context);
    }

    public void Dispose() => _db.Dispose();

    private Product SeedProduct(int id, string name, decimal price, int stock, bool isActive = true)
    {
        var product = new Product { Id = id, Name = name, Price = price, Stock = stock, IsActive = isActive };
        _db.Context.Products.Add(product);
        _db.Context.SaveChanges();
        return product;
    }

    [Fact]
    public async Task CreateOrderAsync_WithValidRequest_DecrementsStockAndPersistsOrder()
    {
        SeedProduct(100, "Laptop", 1200m, stock: 10);
        var request = new CreateOrderRequest
        {
            Items = [new CreateOrderItemRequest { ProductId = 100, Quantity = 3 }]
        };

        var result = await _sut.CreateOrderAsync(request, CancellationToken.None);

        result.Status.ShouldBe(OrderStatus.Pending);
        result.Items.ShouldHaveSingleItem();
        result.Items[0].UnitPrice.ShouldBe(1200m);
        result.Total.ShouldBe(3600m);

        _db.Context.Products.Single(p => p.Id == 100).Stock.ShouldBe(7);
    }

    [Fact]
    public async Task CreateOrderAsync_WithInsufficientStock_ThrowsAndDoesNotChangeStock()
    {
        SeedProduct(100, "Mouse", 50m, stock: 2);
        var request = new CreateOrderRequest
        {
            Items = [new CreateOrderItemRequest { ProductId = 100, Quantity = 5 }]
        };

        await Should.ThrowAsync<InsufficientStockException>(
            () => _sut.CreateOrderAsync(request, CancellationToken.None));

        _db.Context.Products.Single(p => p.Id == 100).Stock.ShouldBe(2);
    }

    [Fact]
    public async Task CreateOrderAsync_WithNonExistentProduct_ThrowsProductNotFoundException()
    {
        var request = new CreateOrderRequest
        {
            Items = [new CreateOrderItemRequest { ProductId = 999, Quantity = 1 }]
        };

        await Should.ThrowAsync<ProductNotFoundException>(
            () => _sut.CreateOrderAsync(request, CancellationToken.None));
    }

    [Fact]
    public async Task CreateOrderAsync_WithInactiveProduct_ThrowsProductInactiveException()
    {
        SeedProduct(100, "Discontinued Item", 10m, stock: 5, isActive: false);
        var request = new CreateOrderRequest
        {
            Items = [new CreateOrderItemRequest { ProductId = 100, Quantity = 1 }]
        };

        await Should.ThrowAsync<ProductInactiveException>(
            () => _sut.CreateOrderAsync(request, CancellationToken.None));
    }

    [Fact]
    public async Task CancelOrderAsync_WhenPending_RestoresStockAndSetsCancelled()
    {
        SeedProduct(100, "Keyboard", 100m, stock: 10);
        var order = await _sut.CreateOrderAsync(new CreateOrderRequest
        {
            Items = [new CreateOrderItemRequest { ProductId = 100, Quantity = 4 }]
        }, CancellationToken.None);
        _db.Context.Products.Single(p => p.Id == 100).Stock.ShouldBe(6);

        await _sut.CancelOrderAsync(order.Id, CancellationToken.None);

        var cancelled = await _sut.GetOrderByIdAsync(order.Id, CancellationToken.None);
        cancelled.Status.ShouldBe(OrderStatus.Cancelled);
        _db.Context.Products.Single(p => p.Id == 100).Stock.ShouldBe(10);
    }

    [Fact]
    public async Task CancelOrderAsync_WhenAlreadyCancelled_ThrowsOrderNotPendingException()
    {
        SeedProduct(100, "Keyboard", 100m, stock: 10);
        var order = await _sut.CreateOrderAsync(new CreateOrderRequest
        {
            Items = [new CreateOrderItemRequest { ProductId = 100, Quantity = 1 }]
        }, CancellationToken.None);
        await _sut.CancelOrderAsync(order.Id, CancellationToken.None);

        await Should.ThrowAsync<OrderNotPendingException>(
            () => _sut.CancelOrderAsync(order.Id, CancellationToken.None));
    }
}
