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
public class ProductCategoryController : ControllerBase
{
    private readonly IProductCategoryService _productCategoryService;

    public ProductCategoryController(IProductCategoryService productCategoryService)
    {
        _productCategoryService = productCategoryService;
    }

    [HttpPost("business/{businessId:guid}")]
    public async Task<IActionResult> Create(
        Guid businessId,
        [FromBody] CreateProductCategoryRequest request)
    {
        var result = await _productCategoryService.CreateAsync(GetUserId(), businessId, request);
        return CreatedAtAction(
            nameof(GetById),
            new { id = result.Id },
            ApiResponse<ProductCategoryResponse>.Ok(
                result,
                "Tạo danh mục sản phẩm thành công.",
                HttpContext.TraceIdentifier));
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateProductCategoryRequest request)
    {
        var result = await _productCategoryService.UpdateAsync(GetUserId(), id, request);
        return Ok(
            ApiResponse<ProductCategoryResponse>.Ok(
                result,
                "Cập nhật danh mục sản phẩm thành công.",
                HttpContext.TraceIdentifier));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(
        Guid id,
        [FromQuery] Guid? fallbackProductCategoryId = null,
        [FromQuery] bool forceDelete = false)
    {
        var activeProducts = await _productCategoryService.GetActiveProductsUsingCategoryAsync(GetUserId(), id);
        if (activeProducts.Any())
        {
            if (!forceDelete && fallbackProductCategoryId == null)
            {
                return Conflict(ApiResponse<List<ProductResponse>>.Fail(
                    activeProducts,
                    "Không thể xóa danh mục sản phẩm này vì có sản phẩm đang sử dụng. Hãy chọn chuyển danh mục hoặc gỡ bỏ.",
                    HttpContext.TraceIdentifier));
            }
        }

        await _productCategoryService.DeleteAsync(GetUserId(), id, fallbackProductCategoryId, forceDelete);
        return Ok(
            ApiResponse<bool>.Ok(
                true,
                "Xóa danh mục sản phẩm thành công.",
                HttpContext.TraceIdentifier));
    }

    [HttpGet("business/{businessId:guid}")]
    public async Task<IActionResult> GetByBusiness(Guid businessId)
    {
        var result = await _productCategoryService.GetByBusinessAsync(GetUserId(), businessId);
        return Ok(
            ApiResponse<IEnumerable<ProductCategoryResponse>>.Ok(
                result,
                "Lấy danh sách danh mục sản phẩm thành công.",
                HttpContext.TraceIdentifier));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var result = await _productCategoryService.GetByIdAsync(GetUserId(), id);
        return Ok(
            ApiResponse<ProductCategoryResponse>.Ok(
                result,
                "Lấy danh mục sản phẩm thành công.",
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
