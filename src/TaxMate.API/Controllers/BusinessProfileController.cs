using Microsoft.AspNetCore.Mvc;
using TaxMate.Model.Common;
using TaxMate.Model.DTO;
using TaxMate.Service.Interfaces;

namespace TaxMate.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class BusinessProfileController : ControllerBase
{
    private readonly IBusinessProfileService _businessProfileService;

    public BusinessProfileController(IBusinessProfileService businessProfileService)
    {
        _businessProfileService = businessProfileService;
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateBusinessProfileRequest request)
    {
        var result = await _businessProfileService.CreateAsync(request);
        return CreatedAtAction(
            nameof(GetById),
            new { id = result.Id },
            ApiResponse<BusinessProfileResponse>.Ok(
                result,
                "Business profile created successfully",
                HttpContext.TraceIdentifier));
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateBusinessProfileRequest request)
    {
        var result = await _businessProfileService.UpdateAsync(id, request);
        return Ok(
            ApiResponse<BusinessProfileResponse>.Ok(
                result,
                "Business profile updated successfully",
                HttpContext.TraceIdentifier));
    }

    [HttpPatch("{id:guid}/deactivate")]
    public async Task<IActionResult> Deactivate(Guid id)
    {
        await _businessProfileService.DeactivateAsync(id);
        return Ok(
            ApiResponse<string>.Ok(
                "Success",
                "Business profile deactivated successfully",
                HttpContext.TraceIdentifier));
    }

    [HttpGet]
    public async Task<IActionResult> GetPaged(
        [FromQuery] Guid ownerId,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? search = null)
    {
        var result = await _businessProfileService.GetPagedAsync(ownerId, pageNumber, pageSize, search);
        return Ok(
            ApiResponse<PagedResult<BusinessProfileResponse>>.Ok(
                result,
                "Get paged business profiles successfully",
                HttpContext.TraceIdentifier));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var result = await _businessProfileService.GetByIdAsync(id);
        return Ok(
            ApiResponse<BusinessProfileResponse>.Ok(
                result,
                "Get business profile successfully",
                HttpContext.TraceIdentifier));
    }
}
