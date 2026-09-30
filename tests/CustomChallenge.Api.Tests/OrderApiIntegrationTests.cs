using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using CustomChallenge.Api.Data;
using CustomChallenge.Api.Domain;
using CustomChallenge.Api.Dtos;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Shouldly;

namespace CustomChallenge.Api.Tests;

// Full-stack integration test: HTTP request -> Controller -> OrderService
// -> EF Core -> a real SQL Server database (the local SQLEXPRESS instance),
// as required by the assignment. Everything else in this suite is a fast
// unit test against SQLite; this is the one test that proves the whole
// pipe actually works end to end.
public class OrderApiIntegrationTests : IAsyncLifetime
{
    private const string ConnectionString =
        "Server=.\\SQLEXPRESS;Database=CustomChallengeOmsIntegrationTests;Trusted_Connection=True;TrustServerCertificate=True;";

    // The API serializes enums as strings (see Program.cs JsonStringEnumConverter);
    // HttpClient's JSON helpers need the same converter to deserialize them back.
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private WebApplicationFactory<Program> _factory = null!;
    private HttpClient _client = null!;

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

        // Starts the host, which runs Database.Migrate() + seed data (Program.cs).
        _client = _factory.CreateClient();
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
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
    public async Task CreatingAndCancellingAnOrder_FlowsThroughEfCoreToSqlServer()
    {
        var products = await _client.GetFromJsonAsync<List<ProductResponse>>("/api/products", JsonOptions);
        var laptop = products!.Single(p => p.Name == "Laptop");

        var createResponse = await _client.PostAsJsonAsync("/api/orders", new CreateOrderRequest
        {
            Items = [new CreateOrderItemRequest { ProductId = laptop.Id, Quantity = 2 }]
        }, JsonOptions);
        createResponse.StatusCode.ShouldBe(HttpStatusCode.Created);

        var order = await createResponse.Content.ReadFromJsonAsync<OrderResponse>(JsonOptions);
        order!.Status.ShouldBe(OrderStatus.Pending);

        var afterCreate = await _client.GetFromJsonAsync<List<ProductResponse>>("/api/products", JsonOptions);
        afterCreate!.Single(p => p.Id == laptop.Id).Stock.ShouldBe(laptop.Stock - 2);

        var cancelResponse = await _client.PostAsync($"/api/orders/{order.Id}/cancel", null);
        cancelResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var detail = await _client.GetFromJsonAsync<OrderResponse>($"/api/orders/{order.Id}", JsonOptions);
        detail!.Status.ShouldBe(OrderStatus.Cancelled);

        var afterCancel = await _client.GetFromJsonAsync<List<ProductResponse>>("/api/products", JsonOptions);
        afterCancel!.Single(p => p.Id == laptop.Id).Stock.ShouldBe(laptop.Stock);
    }
}
