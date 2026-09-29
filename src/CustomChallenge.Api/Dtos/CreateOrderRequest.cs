using System.ComponentModel.DataAnnotations;

namespace CustomChallenge.Api.Dtos;

public class CreateOrderRequest
{
    [Required]
    [MinLength(1, ErrorMessage = "An order must contain at least one item.")]
    public List<CreateOrderItemRequest> Items { get; set; } = [];
}

public class CreateOrderItemRequest
{
    [Required]
    public int ProductId { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "Quantity must be greater than zero.")]
    public int Quantity { get; set; }
}
