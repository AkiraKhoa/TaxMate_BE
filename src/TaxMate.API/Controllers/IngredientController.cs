using Microsoft.AspNetCore.Mvc;
using TaxMate.Model.DTO;
using TaxMate.Service.Interfaces;

namespace TaxMate.API.Controllers;

[Controller]
[Route("api/[controller]")]
public class IngredientController : ControllerBase
{
    private readonly IIngredientService _ingredientService;

    public IngredientController(IIngredientService ingredientService)
    {
        _ingredientService = ingredientService;
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateIngredientRequest request)
    {
        var result = await _ingredientService.CreateAsync(request);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateIngredientRequest request)
    {
        var result = await _ingredientService.UpdateAsync(id, request);
        return Ok(result);
    }

    [HttpPatch("{id:guid}/deactivate")]
    public async Task<IActionResult> Deactivate(Guid id)
    {
        await _ingredientService.DeactivateAsync(id);
        return NoContent();
    }

    [HttpGet]
    public async Task<IActionResult> GetPaged(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? search = null)
    {
        var result = await _ingredientService.GetPagedAsync(pageNumber, pageSize, search);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var result = await _ingredientService.GetByIdAsync(id);
        return Ok(result);
    }
}
