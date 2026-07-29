using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaxMate.Model.Common;
using TaxMate.Model.DTO;
using TaxMate.Model.DTO.Auth;
using TaxMate.Model.DTO.User;
using TaxMate.Service.Interfaces;

namespace TaxMate.API.Controllers;

/// <summary>Quản lý người dùng (Admin).</summary>
[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = UserRoles.Admin)]
public class UserController : ControllerBase
{
    private readonly IUserService _userService;

    public UserController(IUserService userService)
    {
        _userService = userService;
    }

    /// <summary>Tạo người dùng Owner mới.</summary>
    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] AdminCreateUserRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _userService.CreateAsync(request, cancellationToken);
        return Created(
            $"api/User/{result.Id}",
            ApiResponse<UserDto>.Ok(
                result,
                "User created successfully",
                HttpContext.TraceIdentifier));
    }

    /// <summary>Danh sách người dùng (phân trang, tìm kiếm, lọc). Không gồm tài khoản đang đăng nhập.</summary>
    [HttpGet]
    public async Task<IActionResult> GetPaged(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? search = null,
        [FromQuery] string? role = null,
        [FromQuery] string? accountStatus = null,
        CancellationToken cancellationToken = default)
    {
        var result = await _userService.GetPagedAsync(
            pageNumber,
            pageSize,
            search,
            role,
            accountStatus,
            GetUserId(),
            cancellationToken);

        return Ok(
            ApiResponse<PagedResult<AdminUserListItemDto>>.Ok(
                result,
                "Get users successfully",
                HttpContext.TraceIdentifier));
    }

    /// <summary>Lấy chi tiết người dùng theo ID (kèm hồ sơ kinh doanh).</summary>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await _userService.GetByIdAsync(id, cancellationToken);
        return Ok(
            ApiResponse<AdminUserDetailDto>.Ok(
                result,
                "Get user successfully",
                HttpContext.TraceIdentifier));
    }

    /// <summary>Cập nhật thông tin người dùng (một hoặc nhiều trường).</summary>
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] AdminUpdateUserRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _userService.UpdateAsync(id, request, cancellationToken);
        return Ok(
            ApiResponse<UserDto>.Ok(
                result,
                "User updated successfully",
                HttpContext.TraceIdentifier));
    }

    /// <summary>Bật/tắt trạng thái tài khoản (Active ↔ Inactive).</summary>
    [HttpPatch("{id:guid}/toggle-status")]
    public async Task<IActionResult> ToggleStatus(Guid id, CancellationToken cancellationToken)
    {
        var result = await _userService.ToggleStatusAsync(id, GetUserId(), cancellationToken);
        return Ok(
            ApiResponse<UserDto>.Ok(
                result,
                "User status updated successfully",
                HttpContext.TraceIdentifier));
    }

    private Guid GetUserId()
    {
        var sub = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("sub");

        if (sub is null || !Guid.TryParse(sub, out var userId))
        {
            throw new UnauthorizedAccessException();
        }

        return userId;
    }
}
