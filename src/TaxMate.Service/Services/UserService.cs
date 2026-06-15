using TaxMate.Model.Common;
using TaxMate.Model.DTO.Auth;
using TaxMate.Model.DTO.User;
using TaxMate.Model.Entities;
using TaxMate.Repository.Interfaces;
using TaxMate.Service.Common;
using TaxMate.Service.Exceptions;
using TaxMate.Service.Interfaces;

namespace TaxMate.Service.Services;

public class UserService : IUserService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IGenericRepository<User> _users;
    private readonly IPasswordHasher _passwordHasher;

    public UserService(
        IUnitOfWork unitOfWork,
        IGenericRepository<User> users,
        IPasswordHasher passwordHasher)
    {
        _unitOfWork = unitOfWork;
        _users = users;
        _passwordHasher = passwordHasher;
    }

    public async Task<UserDto> CreateAsync(
        AdminCreateUserRequest request,
        CancellationToken cancellationToken = default)
    {
        UserInputValidator.ValidateTaxCode(request.TaxCode);
        UserInputValidator.ValidatePhone(request.Phone);
        UserInputValidator.ValidateEmail(request.Email);
        UserInputValidator.ValidatePassword(request.Password);

        var normalizedEmail = request.Email.Trim().ToLowerInvariant();
        var normalizedPhone = request.Phone.Trim();

        await EnsureEmailAvailableAsync(normalizedEmail, excludeUserId: null, cancellationToken);
        await EnsurePhoneAvailableAsync(normalizedPhone, excludeUserId: null, cancellationToken);
        await EnsureTaxCodeAvailableAsync(request.TaxCode, excludeUserId: null, cancellationToken);

        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = normalizedEmail,
            FullName = request.FullName.Trim(),
            TaxCode = request.TaxCode,
            Phone = normalizedPhone,
            PasswordHash = _passwordHasher.Hash(request.Password),
            AccountStatus = AccountStatus.Active,
            Role = UserRoles.Owner
        };

        await _users.AddAsync(user);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return MapToDto(user);
    }

    public async Task<IEnumerable<UserDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var users = await _users.GetAllAsync();
        return users.Select(MapToDto);
    }

    public async Task<UserDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var user = await _users.GetByIdAsync(id);
        if (user is null)
        {
            throw new NotFoundException("Không tìm thấy người dùng.");
        }

        return MapToDto(user);
    }

    public async Task<UserDto> UpdateAsync(
        Guid id,
        AdminUpdateUserRequest request,
        CancellationToken cancellationToken = default)
    {
        var user = await _users.GetByIdAsync(id);
        if (user is null)
        {
            throw new NotFoundException("Không tìm thấy người dùng.");
        }

        if (!HasAnyUpdateField(request))
        {
            throw new BadRequestException("Phải cung cấp ít nhất một trường để cập nhật.");
        }

        if (!string.IsNullOrWhiteSpace(request.Email))
        {
            UserInputValidator.ValidateEmail(request.Email);
            var normalizedEmail = request.Email.Trim().ToLowerInvariant();
            await EnsureEmailAvailableAsync(normalizedEmail, id, cancellationToken);
            user.Email = normalizedEmail;
        }

        if (!string.IsNullOrWhiteSpace(request.TaxCode))
        {
            UserInputValidator.ValidateTaxCode(request.TaxCode);
            await EnsureTaxCodeAvailableAsync(request.TaxCode, id, cancellationToken);
            user.TaxCode = request.TaxCode;
        }

        if (!string.IsNullOrWhiteSpace(request.Phone))
        {
            UserInputValidator.ValidatePhone(request.Phone);
            var normalizedPhone = request.Phone.Trim();
            await EnsurePhoneAvailableAsync(normalizedPhone, id, cancellationToken);
            user.Phone = normalizedPhone;
        }

        if (!string.IsNullOrWhiteSpace(request.Password))
        {
            UserInputValidator.ValidatePassword(request.Password);
            user.PasswordHash = _passwordHasher.Hash(request.Password);
        }

        if (!string.IsNullOrWhiteSpace(request.FullName))
        {
            user.FullName = request.FullName.Trim();
        }

        if (request.AvatarUrl is not null)
        {
            user.AvatarUrl = string.IsNullOrWhiteSpace(request.AvatarUrl)
                ? null
                : request.AvatarUrl.Trim();
        }

        _users.Update(user);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return MapToDto(user);
    }

    private static bool HasAnyUpdateField(AdminUpdateUserRequest request) =>
        !string.IsNullOrWhiteSpace(request.FullName)
        || !string.IsNullOrWhiteSpace(request.TaxCode)
        || !string.IsNullOrWhiteSpace(request.Phone)
        || !string.IsNullOrWhiteSpace(request.Email)
        || !string.IsNullOrWhiteSpace(request.Password)
        || request.AvatarUrl is not null;

    private async Task EnsureEmailAvailableAsync(
        string email,
        Guid? excludeUserId,
        CancellationToken cancellationToken)
    {
        var existing = excludeUserId.HasValue
            ? await _users.FirstOrDefaultAsync(u => u.Email == email && u.Id != excludeUserId.Value)
            : await _users.FirstOrDefaultAsync(u => u.Email == email);

        if (existing is null)
        {
            return;
        }

        if (existing.PasswordHash is null)
        {
            throw new InvalidOperationException(
                "Email đã được đăng ký qua Google. Vui lòng đăng nhập bằng Google.");
        }

        throw new InvalidOperationException("Email đã được sử dụng.");
    }

    private async Task EnsurePhoneAvailableAsync(
        string phone,
        Guid? excludeUserId,
        CancellationToken cancellationToken)
    {
        var existing = excludeUserId.HasValue
            ? await _users.FirstOrDefaultAsync(u => u.Phone == phone && u.Id != excludeUserId.Value)
            : await _users.FirstOrDefaultAsync(u => u.Phone == phone);

        if (existing is not null)
        {
            throw new InvalidOperationException("Số điện thoại đã được sử dụng.");
        }
    }

    private async Task EnsureTaxCodeAvailableAsync(
        string taxCode,
        Guid? excludeUserId,
        CancellationToken cancellationToken)
    {
        var existing = excludeUserId.HasValue
            ? await _users.FirstOrDefaultAsync(u => u.TaxCode == taxCode && u.Id != excludeUserId.Value)
            : await _users.FirstOrDefaultAsync(u => u.TaxCode == taxCode);

        if (existing is not null)
        {
            throw new InvalidOperationException("Số căn cước công dân đã được sử dụng.");
        }
    }

    private static UserDto MapToDto(User user) => new()
    {
        Id = user.Id,
        Email = user.Email,
        FullName = user.FullName,
        AvatarUrl = user.AvatarUrl,
        AccountStatus = user.AccountStatus,
        Role = user.Role,
        TaxCode = user.TaxCode,
        Phone = user.Phone,
        HasProfileInfo = !string.IsNullOrWhiteSpace(user.TaxCode)
            && !string.IsNullOrWhiteSpace(user.Phone)
    };
}
