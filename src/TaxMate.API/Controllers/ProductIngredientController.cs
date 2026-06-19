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
public class ProductIngredientController : ControllerBase
{
    private readonly IProductIngredientService _productIngredientService;

    public ProductIngredientController(IProductIngredientService productIngredientService)
    {
        _productIngredientService = productIngredientService;
    }

    [HttpGet("product/{productId:guid}")]
    public async Task<IActionResult> GetByProduct(Guid productId)
    {
        var result = await _productIngredientService.GetByProductIdAsync(GetUserId(), productId);
        return Ok(
            ApiResponse<IEnumerable<ProductIngredientResponse>>.Ok(
                result,
                "Get product ingredients successfully",
                HttpContext.TraceIdentifier));
    }

    [HttpPost("product/{productId:guid}")]
    public async Task<IActionResult> Add(
        Guid productId,
        [FromBody] AddProductIngredientRequest request)
    {
        var result = await _productIngredientService.AddAsync(GetUserId(), productId, request);
        return Created(
            $"/api/ProductIngredient/product/{productId}/ingredient/{result.IngredientId}",
            ApiResponse<ProductIngredientResponse>.Ok(
                result,
                "Product ingredient linked successfully",
                HttpContext.TraceIdentifier));
    }

    [HttpPut("product/{productId:guid}/ingredient/{ingredientId:guid}")]
    public async Task<IActionResult> Update(
        Guid productId,
        Guid ingredientId,
        [FromBody] UpdateProductIngredientRequest request)
    {
        var result = await _productIngredientService.UpdateAsync(
            GetUserId(),
            productId,
            ingredientId,
            request);
        return Ok(
            ApiResponse<ProductIngredientResponse>.Ok(
                result,
                "Product ingredient updated successfully",
                HttpContext.TraceIdentifier));
    }

    [HttpDelete("product/{productId:guid}/ingredient/{ingredientId:guid}")]
    public async Task<IActionResult> Delete(Guid productId, Guid ingredientId)
    {
        await _productIngredientService.DeleteAsync(GetUserId(), productId, ingredientId);
        return Ok(
            ApiResponse<string>.Ok(
                "Success",
                "Product ingredient unlinked successfully",
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
