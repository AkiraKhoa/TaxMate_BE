using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaxMate.Model.Common;
using TaxMate.Model.DTO;
using TaxMate.Service.Interfaces;

namespace TaxMate.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = UserRoles.Owner)]
[Authorize(Policy = AuthPolicies.ActiveAccountOnly)]
public class ProductPriceController : ControllerBase
{
    private readonly IProductPriceService _productPriceService;

    public ProductPriceController(IProductPriceService productPriceService)
    {
        _productPriceService = productPriceService;
    }

    [HttpPost("product/{productId:guid}")]
    public async Task<IActionResult> Create(
        Guid productId,
        [FromBody] CreateProductPriceRequest request)
    {
        var result = await _productPriceService.CreateAsync(GetUserId(), productId, request);
        return CreatedAtAction(
            nameof(GetById),
            new { id = result.Id },
            ApiResponse<ProductPriceResponse>.Ok(
                result,
                "Product price created successfully",
                HttpContext.TraceIdentifier));
    }

    [HttpGet("product/{productId:guid}")]
    public async Task<IActionResult> GetByProduct(Guid productId)
    {
        var result = await _productPriceService.GetByProductIdAsync(GetUserId(), productId);
        return Ok(
            ApiResponse<IEnumerable<ProductPriceResponse>>.Ok(
                result,
                "Get product prices successfully",
                HttpContext.TraceIdentifier));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var result = await _productPriceService.GetByIdAsync(GetUserId(), id);
        return Ok(
            ApiResponse<ProductPriceResponse>.Ok(
                result,
                "Get product price successfully",
                HttpContext.TraceIdentifier));
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateProductPriceRequest request)
    {
        var result = await _productPriceService.UpdateAsync(GetUserId(), id, request);
        return Ok(
            ApiResponse<ProductPriceResponse>.Ok(
                result,
                "Product price updated successfully",
                HttpContext.TraceIdentifier));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        await _productPriceService.DeleteAsync(GetUserId(), id);
        return Ok(
            ApiResponse<string>.Ok(
                "Success",
                "Product price deleted successfully",
                HttpContext.TraceIdentifier));
    }

    private Guid GetUserId()
    {
        var sub = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("sub");

        if (sub is null || !Guid.TryParse(sub, out var userId))
            throw new UnauthorizedAccessException("Token invalid.");

        return userId;
    }
}
