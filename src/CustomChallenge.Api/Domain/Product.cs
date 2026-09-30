namespace CustomChallenge.Api.Domain;

public class Product
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public int Stock { get; set; }
    public bool IsActive { get; set; }

    // App-managed concurrency token (bumped in AppDbContext.SaveChanges):
    // prevents overselling when two requests decrement stock for the same
    // product at the same time (see PLAN.md). Chosen over a DB-generated
    // rowversion column so behavior is identical across providers (SQL
    // Server in production, SQLite in tests).
    public int Version { get; set; }
}
