using TaxMate.Model.DTO.Auth;
using TaxMate.Model.DTO.User;

namespace TaxMate.Service.Interfaces;

public interface IUserService
{
    Task<UserDto> CreateAsync(AdminCreateUserRequest request, CancellationToken cancellationToken = default);

    Task<IEnumerable<UserDto>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<UserDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<UserDto> UpdateAsync(Guid id, AdminUpdateUserRequest request, CancellationToken cancellationToken = default);
}
