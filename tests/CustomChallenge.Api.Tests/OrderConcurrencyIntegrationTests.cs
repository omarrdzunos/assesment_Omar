using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using CustomChallenge.Api.Data;
using CustomChallenge.Api.Dtos;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Shouldly;

namespace CustomChallenge.Api.Tests;

// Proves the atomic-conditional-update strategy in OrderService actually
// holds under real concurrency, against a real SQL Server database — not
// just that the SQL looks right. Ten requests race for five units of stock;
// exactly five must win.
public class OrderConcurrencyIntegrationTests : IAsyncLifetime
{
    private const string ConnectionString =
        "Server=.\\SQLEXPRESS;Database=CustomChallengeOmsConcurrencyTests;Trusted_Connection=True;TrustServerCertificate=True;";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private WebApplicationFactory<Program> _factory = null!;

    public async Task InitializeAsync()
    {
        await DropTestDatabaseAsync();

        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:DefaultConnection"] = ConnectionString
                });
            });
        });

        // Force host startup (Database.Migrate() + seed, see Program.cs).
        using var warmup = _factory.CreateClient();
    }

    public async Task DisposeAsync()
    {
        _factory.Dispose();
        await DropTestDatabaseAsync();
    }

    private static async Task DropTestDatabaseAsync()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlServer(ConnectionString).Options;
        await using var context = new AppDbContext(options);
        await context.Database.EnsureDeletedAsync();
    }

    [Fact]
    public async Task ConcurrentOrders_ForTheSameProduct_NeverOversellStock()
    {
        using var client = _factory.CreateClient();

        var products = await client.GetFromJsonAsync<List<ProductResponse>>("/api/products", JsonOptions);
        var laptop = products!.Single(p => p.Name == "Laptop"); // seeded with Stock = 10

        // Bring stock down to a known, small number (5) through the public
        // API, rather than reaching into the database directly.
        var setup = await client.PostAsJsonAsync("/api/orders", new CreateOrderRequest
        {
            Items = [new CreateOrderItemRequest { ProductId = laptop.Id, Quantity = laptop.Stock - 5 }]
        }, JsonOptions);
        setup.EnsureSuccessStatusCode();

        // 10 concurrent requests for 1 unit each, on the same shared
        // HttpClient (safe: HttpClient supports concurrent in-flight
        // requests) — only 5 of these can possibly succeed.
        var responses = await Task.WhenAll(Enumerable.Range(0, 10).Select(_ =>
            client.PostAsJsonAsync("/api/orders", new CreateOrderRequest
            {
                Items = [new CreateOrderItemRequest { ProductId = laptop.Id, Quantity = 1 }]
            }, JsonOptions)));

        responses.Count(r => r.StatusCode == HttpStatusCode.Created).ShouldBe(5);
        responses.Count(r => r.StatusCode == HttpStatusCode.Conflict).ShouldBe(5);

        var finalProducts = await client.GetFromJsonAsync<List<ProductResponse>>("/api/products", JsonOptions);
        finalProducts!.Single(p => p.Id == laptop.Id).Stock.ShouldBe(0);
    }
}
