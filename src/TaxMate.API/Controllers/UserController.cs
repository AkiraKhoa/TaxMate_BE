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

    /// <summary>Danh sách tất cả người dùng.</summary>
    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        var result = await _userService.GetAllAsync(cancellationToken);
        return Ok(
            ApiResponse<IEnumerable<UserDto>>.Ok(
                result,
                "Get users successfully",
                HttpContext.TraceIdentifier));
    }

    /// <summary>Lấy chi tiết người dùng theo ID.</summary>
    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await _userService.GetByIdAsync(id, cancellationToken);
        return Ok(
            ApiResponse<UserDto>.Ok(
                result,
                "Get user successfully",
                HttpContext.TraceIdentifier));
    }

    /// <summary>Cập nhật thông tin người dùng (một hoặc nhiều trường).</summary>
    [HttpPut("{id}")]
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
}
