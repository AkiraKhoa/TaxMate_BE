using TaxMate.Model.Common;
using TaxMate.Model.DTO.Auth;
using TaxMate.Model.DTO.User;

namespace TaxMate.Service.Interfaces;

public interface IUserService
{
    Task<UserDto> CreateAsync(AdminCreateUserRequest request, CancellationToken cancellationToken = default);

    Task<PagedResult<AdminUserListItemDto>> GetPagedAsync(
        int pageNumber,
        int pageSize,
        string? search,
        string? role,
        string? accountStatus,
        Guid excludeUserId,
        CancellationToken cancellationToken = default);

    Task<AdminUserDetailDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<UserDto> UpdateAsync(Guid id, AdminUpdateUserRequest request, CancellationToken cancellationToken = default);

    Task<UserDto> ToggleStatusAsync(Guid id, Guid currentUserId, CancellationToken cancellationToken = default);
}
