namespace CustomChallenge.Api.Domain;

public class OrderItem
{
    public int Id { get; set; }

    public int OrderId { get; set; }
    public Order? Order { get; set; }

    public int ProductId { get; set; }
    public Product? Product { get; set; }

    public int Quantity { get; set; }

    // Snapshot of Product.Price at the moment the order was placed —
    // must not change retroactively if the product's price changes later.
    public decimal UnitPrice { get; set; }
}
