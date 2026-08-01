using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaxMate.Model.Common;
using TaxMate.Model.DTO;
using TaxMate.Service.Interfaces;

namespace TaxMate.API.Controllers;

[ApiController]
[Route("api/business-categories")]
[Authorize(Roles = UserRoles.Owner)]
[Authorize(Policy = AuthPolicies.ActiveAccountOnly)]
public class BusinessCategoryController : ControllerBase
{
    private readonly IBusinessCategoryService _businessCategoryService;

    public BusinessCategoryController(IBusinessCategoryService businessCategoryService)
    {
        _businessCategoryService = businessCategoryService;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var result = await _businessCategoryService.GetAllAsync();
        return Ok(
            ApiResponse<List<BusinessCategoryResponse>>.Ok(
                result,
                "Get business categories successfully",
                HttpContext.TraceIdentifier));
    }
}
