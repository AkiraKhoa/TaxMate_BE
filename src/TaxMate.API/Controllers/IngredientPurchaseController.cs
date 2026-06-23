using Microsoft.AspNetCore.Mvc;
using TaxMate.Model.Common;
using TaxMate.Model.DTO;
using TaxMate.Service.Interfaces;

namespace TaxMate.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class IngredientPurchaseController : ControllerBase
{
    private readonly IIngredientPurchaseService _purchaseService;

    public IngredientPurchaseController(IIngredientPurchaseService purchaseService)
    {
        _purchaseService = purchaseService;
    }

    /// <summary>Creates a new ingredient purchase record scoped to a business.</summary>
    [HttpPost("business/{businessId:guid}")]
    public async Task<IActionResult> Create(Guid businessId, [FromBody] CreateIngredientPurchaseRequest request)
    {
        var result = await _purchaseService.CreateAsync(businessId, request);
        return CreatedAtAction(
            nameof(GetById),
            new { id = result.Id },
            ApiResponse<IngredientPurchaseResponse>.Ok(
                result,
                "Ingredient purchase record created successfully",
                HttpContext.TraceIdentifier));
    }

    /// <summary>Creates a batch of ingredient purchase records scoped to a business.</summary>
    [HttpPost("business/{businessId:guid}/batch")]
    public async Task<IActionResult> CreateBatch(Guid businessId, [FromBody] CreateBatchIngredientPurchaseRequest request)
    {
        var result = await _purchaseService.CreateBatchAsync(businessId, request);
        return Ok(
            ApiResponse<IEnumerable<IngredientPurchaseResponse>>.Ok(
                result,
                "Ingredient purchase records created successfully",
                HttpContext.TraceIdentifier));
    }

    /// <summary>Updates an existing ingredient purchase record.</summary>
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateIngredientPurchaseRequest request)
    {
        var result = await _purchaseService.UpdateAsync(id, request);
        return Ok(
            ApiResponse<IngredientPurchaseResponse>.Ok(
                result,
                "Ingredient purchase record updated successfully",
                HttpContext.TraceIdentifier));
    }

    /// <summary>Deletes an ingredient purchase record.</summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        await _purchaseService.DeleteAsync(id);
        return Ok(
            ApiResponse<string>.Ok(
                "Success",
                "Ingredient purchase record deleted successfully",
                HttpContext.TraceIdentifier));
    }

    /// <summary>Gets an ingredient purchase record by its ID.</summary>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var result = await _purchaseService.GetByIdAsync(id);
        return Ok(
            ApiResponse<IngredientPurchaseResponse>.Ok(
                result,
                "Ingredient purchase record retrieved successfully",
                HttpContext.TraceIdentifier));
    }

    /// <summary>Gets a paginated list of ingredient purchase records for a business.</summary>
    [HttpGet("business/{businessId:guid}")]
    public async Task<IActionResult> GetPaged(
        Guid businessId,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? search = null)
    {
        var result = await _purchaseService.GetPagedByBusinessAsync(businessId, pageNumber, pageSize, search);
        return Ok(
            ApiResponse<PagedResult<IngredientPurchaseResponse>>.Ok(
                result,
                "Ingredient purchase records retrieved successfully",
                HttpContext.TraceIdentifier));
    }
}
