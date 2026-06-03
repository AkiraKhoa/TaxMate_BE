using Microsoft.AspNetCore.Mvc;
using TaxMate.Model.DTO;
using TaxMate.Service.Interfaces;

namespace TaxMate.API.Controllers;

[Controller]
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
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateBusinessProfileRequest request)
    {
        var result = await _businessProfileService.UpdateAsync(id, request);
        return Ok(result);
    }

    [HttpPatch("{id:guid}/deactivate")]
    public async Task<IActionResult> Deactivate(Guid id)
    {
        await _businessProfileService.DeactivateAsync(id);
        return NoContent();
    }

    [HttpGet]
    public async Task<IActionResult> GetPaged(
        [FromQuery] Guid ownerId,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? search = null)
    {
        var result = await _businessProfileService.GetPagedAsync(ownerId, pageNumber, pageSize, search);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var result = await _businessProfileService.GetByIdAsync(id);
        return Ok(result);
    }
}
