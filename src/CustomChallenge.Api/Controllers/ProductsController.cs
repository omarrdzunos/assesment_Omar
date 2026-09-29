using CustomChallenge.Api.Dtos;
using CustomChallenge.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace CustomChallenge.Api.Controllers;

[ApiController]
[Route("api/products")]
public class ProductsController(IProductService productService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ProductResponse>>> GetAll(CancellationToken ct)
    {
        return Ok(await productService.GetAllAsync(ct));
    }
}
