namespace CustomChallenge.Api.Domain;

public class Order
{
    public int Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public OrderStatus Status { get; set; }

    public List<OrderItem> Items { get; set; } = [];
}
