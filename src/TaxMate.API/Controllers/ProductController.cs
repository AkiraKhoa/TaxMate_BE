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
public class ProductController : ControllerBase
{
    private readonly IProductService _productService;

    public ProductController(IProductService productService)
    {
        _productService = productService;
    }

    [HttpPost("business/{businessId:guid}")]
    public async Task<IActionResult> Create(
        Guid businessId,
        [FromBody] CreateProductRequest request)
    {
        var result = await _productService.CreateAsync(GetUserId(), businessId, request);
        return CreatedAtAction(
            nameof(GetById),
            new { id = result.Id },
            ApiResponse<ProductResponse>.Ok(
                result,
                "Product created successfully",
                HttpContext.TraceIdentifier));
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateProductRequest request)
    {
        var result = await _productService.UpdateAsync(GetUserId(), id, request);
        return Ok(
            ApiResponse<ProductResponse>.Ok(
                result,
                "Product updated successfully",
                HttpContext.TraceIdentifier));
    }

    [HttpPatch("{id:guid}/cost-price")]
    public async Task<IActionResult> UpdateCostPrice(Guid id, [FromBody] UpdateProductCostPriceRequest request)
    {
        var result = await _productService.UpdateCostPriceAsync(GetUserId(), id, request);
        return Ok(
            ApiResponse<ProductResponse>.Ok(
                result,
                "Product cost price and stock updated successfully",
                HttpContext.TraceIdentifier));
    }

    [HttpPatch("{id:guid}/toggle-status")]
    public async Task<IActionResult> ToggleStatus(Guid id)
    {
        var result = await _productService.ToggleStatusAsync(GetUserId(), id);
        return Ok(
            ApiResponse<ProductResponse>.Ok(
                result,
                "Product status toggled successfully",
                HttpContext.TraceIdentifier));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        await _productService.DeleteAsync(GetUserId(), id);
        return Ok(
            ApiResponse<object?>.Ok(
                null,
                "Product deleted successfully",
                HttpContext.TraceIdentifier));
    }

    [HttpGet("business/{businessId:guid}")]
    public async Task<IActionResult> GetPaged(
        Guid businessId,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? search = null,
        [FromQuery] string? status = null,
        [FromQuery] Guid? productCategoryId = null)
    {
        var result = await _productService.GetPagedByBusinessAsync(
            GetUserId(), businessId, pageNumber, pageSize, search, status, productCategoryId);
        return Ok(
            ApiResponse<PagedResult<ProductResponse>>.Ok(
                result,
                "Get paged products successfully",
                HttpContext.TraceIdentifier));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var result = await _productService.GetByIdAsync(GetUserId(), id);
        return Ok(
            ApiResponse<ProductResponse>.Ok(
                result,
                "Get product successfully",
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
