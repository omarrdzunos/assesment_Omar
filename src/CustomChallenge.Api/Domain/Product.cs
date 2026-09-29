namespace CustomChallenge.Api.Domain;

public class Product
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public int Stock { get; set; }
    public bool IsActive { get; set; }

    // Concurrency token: prevents overselling when two requests decrement
    // stock for the same product at the same time (see PLAN.md).
    public byte[] RowVersion { get; set; } = [];
}
