using CustomChallenge.Api.Dtos;
using CustomChallenge.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace CustomChallenge.Api.Controllers;

[ApiController]
[Route("api/orders")]
public class OrdersController(IOrderService orderService) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<OrderResponse>> Create(CreateOrderRequest request, CancellationToken ct)
    {
        var order = await orderService.CreateOrderAsync(request, ct);
        return CreatedAtAction(nameof(GetById), new { id = order.Id }, order);
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<OrderSummaryResponse>>> GetAll(CancellationToken ct)
    {
        return Ok(await orderService.GetOrdersAsync(ct));
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<OrderResponse>> GetById(int id, CancellationToken ct)
    {
        return Ok(await orderService.GetOrderByIdAsync(id, ct));
    }

    [HttpPost("{id:int}/cancel")]
    public async Task<IActionResult> Cancel(int id, CancellationToken ct)
    {
        await orderService.CancelOrderAsync(id, ct);
        return NoContent();
    }
}
