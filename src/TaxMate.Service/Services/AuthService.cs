using System.Security.Cryptography;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using TaxMate.Model.Common;
using TaxMate.Model.DTO.Auth;
using TaxMate.Model.Entities;
using TaxMate.Repository.Interfaces;
using TaxMate.Service.Common;
using TaxMate.Service.Exceptions;
using TaxMate.Service.Interfaces;

namespace TaxMate.Service.Services;

public class AuthService : IAuthService
{
    private readonly int _verificationTokenExpiryMinutes;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IGenericRepository<User> _users;
    private readonly IGoogleTokenValidator _googleTokenValidator;
    private readonly IJwtService _jwtService;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IEmailService _emailService;
    private readonly ILogger<AuthService> _logger;

    public AuthService(
        IUnitOfWork unitOfWork,
        IGenericRepository<User> users,
        IGoogleTokenValidator googleTokenValidator,
        IJwtService jwtService,
        IPasswordHasher passwordHasher,
        IEmailService emailService,
        ILogger<AuthService> logger,
        IConfiguration configuration)
    {
        _unitOfWork = unitOfWork;
        _users = users;
        _googleTokenValidator = googleTokenValidator;
        _jwtService = jwtService;
        _passwordHasher = passwordHasher;
        _emailService = emailService;
        _logger = logger;
        _verificationTokenExpiryMinutes = configuration.GetValue(
            "App:VerificationTokenExpiryMinutes",
            1440);
    }

    public async Task<AuthResponse> RegisterAsync(
        RegisterRequest request,
        CancellationToken cancellationToken = default)
    {
        UserInputValidator.ValidateTaxCode(request.TaxCode);
        UserInputValidator.ValidatePhone(request.Phone);
        UserInputValidator.ValidateEmail(request.Email);
        UserInputValidator.ValidatePassword(request.Password);

        var userRepo = _users;
        var normalizedEmail = request.Email.Trim().ToLowerInvariant();
        var normalizedPhone = request.Phone.Trim();

        await EnsureEmailAvailableAsync(userRepo, normalizedEmail, cancellationToken);
        await EnsurePhoneAvailableAsync(userRepo, normalizedPhone, cancellationToken);
        await EnsureTaxCodeAvailableAsync(userRepo, request.TaxCode, cancellationToken);

        var (token, expiresAt) = CreateVerificationToken();
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = normalizedEmail,
            FullName = request.FullName.Trim(),
            TaxCode = request.TaxCode,
            Phone = normalizedPhone,
            PasswordHash = _passwordHasher.Hash(request.Password),
            AccountStatus = AccountStatus.Pending,
            Role = "Owner",
            EmailVerificationToken = token,
            EmailVerificationTokenExpiresAt = expiresAt
        };

        await userRepo.AddAsync(user);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await TrySendVerificationEmailAsync(user, cancellationToken);

        var (accessToken, jwtExpiresAt) = _jwtService.GenerateToken(user);

        return new AuthResponse
        {
            AccessToken = accessToken,
            ExpiresAt = jwtExpiresAt,
            User = MapToDto(user),
            RequiresEmailVerification = true
        };
    }

    public async Task<AuthResponse> LoginWithPasswordAsync(
        LoginRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Login) || string.IsNullOrWhiteSpace(request.Password))
        {
            throw new InvalidCredentialsException();
        }

        var userRepo = _users;
        var login = request.Login.Trim();
        var user = login.Contains('@')
            ? await userRepo.FirstOrDefaultAsync(u => u.Email == login.ToLowerInvariant())
            : await userRepo.FirstOrDefaultAsync(u => u.Phone == login);

        if (user is null || string.IsNullOrEmpty(user.PasswordHash))
        {
            throw new InvalidCredentialsException();
        }

        if (!_passwordHasher.Verify(request.Password, user.PasswordHash))
        {
            throw new InvalidCredentialsException();
        }

        if (user.AccountStatus == AccountStatus.Inactive)
        {
            throw new AccountInactiveException();
        }

        if (user.AccountStatus == AccountStatus.Pending)
        {
            await EnsurePendingVerificationTokenAsync(user, userRepo, cancellationToken);

            var (pendingToken, pendingExpiresAt) = _jwtService.GenerateToken(user);

            return new AuthResponse
            {
                AccessToken = pendingToken,
                ExpiresAt = pendingExpiresAt,
                User = MapToDto(user),
                RequiresEmailVerification = true
            };
        }

        var (accessToken, jwtExpiresAt) = _jwtService.GenerateToken(user);

        return new AuthResponse
        {
            AccessToken = accessToken,
            ExpiresAt = jwtExpiresAt,
            User = MapToDto(user),
            RequiresEmailVerification = false
        };
    }

    public async Task<AuthResponse> LoginWithGoogleAsync(
        string idToken,
        CancellationToken cancellationToken = default)
    {
        var googleUser = await _googleTokenValidator.ValidateAsync(idToken, cancellationToken);
        var userRepo = _users;

        var user = await userRepo.FirstOrDefaultAsync(u => u.GoogleId == googleUser.GoogleId)
            ?? await userRepo.FirstOrDefaultAsync(u => u.Email == googleUser.Email);

        var isNewUser = user is null;

        if (isNewUser)
        {
            var (token, expiresAt) = CreateVerificationToken();
            user = new User
            {
                Id = Guid.NewGuid(),
                Email = googleUser.Email,
                GoogleId = googleUser.GoogleId,
                FullName = googleUser.FullName,
                AvatarUrl = googleUser.AvatarUrl,
                AccountStatus = AccountStatus.Pending,
                PasswordHash = null,
                Role = "Owner",
                EmailVerificationToken = token,
                EmailVerificationTokenExpiresAt = expiresAt
            };

            await userRepo.AddAsync(user);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            await TrySendVerificationEmailAsync(user, cancellationToken);
        }
        else
        {
            if (user!.AccountStatus == AccountStatus.Inactive)
            {
                throw new AccountInactiveException();
            }

            user.GoogleId ??= googleUser.GoogleId;
            user.FullName = googleUser.FullName;
            user.AvatarUrl = googleUser.AvatarUrl;

            if (user.AccountStatus == AccountStatus.Pending)
            {
                await EnsurePendingVerificationTokenAsync(user, userRepo, cancellationToken);
            }
            else
            {
                userRepo.Update(user);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }
        }

        var (accessToken, jwtExpiresAt) = _jwtService.GenerateToken(user);

        return new AuthResponse
        {
            AccessToken = accessToken,
            ExpiresAt = jwtExpiresAt,
            User = MapToDto(user),
            RequiresEmailVerification = user.AccountStatus == AccountStatus.Pending
        };
    }

    public async Task<string> ConfirmEmailVerificationAsync(
        string token,
        CancellationToken cancellationToken = default)
    {
        var user = await ActivateUserFromVerificationTokenAsync(token, cancellationToken);
        return user.Email;
    }

    public async Task<VerifyEmailResponse> CompleteEmailVerificationAsync(
        string token,
        CancellationToken cancellationToken = default)
    {
        var user = await ActivateUserFromVerificationTokenAsync(token, cancellationToken);
        var (accessToken, expiresAt) = _jwtService.GenerateToken(user);

        return new VerifyEmailResponse
        {
            Message = "Email đã được xác minh thành công.",
            User = MapToDto(user),
            AccessToken = accessToken,
            ExpiresAt = expiresAt
        };
    }

    public async Task ResendVerificationEmailAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var userRepo = _users;
        var user = await userRepo.GetByIdAsync(userId)
            ?? throw new KeyNotFoundException("Không tìm thấy người dùng.");

        if (user.AccountStatus != AccountStatus.Pending)
        {
            throw new InvalidOperationException("Tài khoản đã được kích hoạt.");
        }

        var (token, expiresAt) = CreateVerificationToken();
        user.EmailVerificationToken = token;
        user.EmailVerificationTokenExpiresAt = expiresAt;

        userRepo.Update(user);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await _emailService.SendVerificationEmailAsync(
            user.Email,
            user.FullName,
            token,
            cancellationToken);
    }

    public async Task<UserDto> GetCurrentUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var user = await _users.GetByIdAsync(userId)
            ?? throw new KeyNotFoundException("Không tìm thấy người dùng.");

        return MapToDto(user);
    }

    private async Task EnsurePendingVerificationTokenAsync(
        User user,
        IGenericRepository<User> userRepo,
        CancellationToken cancellationToken)
    {
        var needsNewToken = string.IsNullOrEmpty(user.EmailVerificationToken)
            || user.EmailVerificationTokenExpiresAt is null
            || DateTime.UtcNow > user.EmailVerificationTokenExpiresAt;

        if (needsNewToken)
        {
            var (token, expiresAt) = CreateVerificationToken();
            user.EmailVerificationToken = token;
            user.EmailVerificationTokenExpiresAt = expiresAt;
        }

        userRepo.Update(user);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        if (needsNewToken)
        {
            await TrySendVerificationEmailAsync(user, cancellationToken);
        }
    }

    private async Task TrySendVerificationEmailAsync(
        User user,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(user.EmailVerificationToken))
        {
            return;
        }

        try
        {
            await _emailService.SendVerificationEmailAsync(
                user.Email,
                user.FullName,
                user.EmailVerificationToken,
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send verification email to {Email}", user.Email);
        }
    }

    private async Task<User> ActivateUserFromVerificationTokenAsync(
        string token,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            throw new ArgumentException("Token xác minh không hợp lệ.");
        }

        var userRepo = _users;
        var user = await userRepo.FirstOrDefaultAsync(u => u.EmailVerificationToken == token);

        if (user is null)
        {
            throw new ArgumentException("Liên kết xác minh không hợp lệ hoặc đã được sử dụng.");
        }

        if (user.EmailVerificationTokenExpiresAt is null
            || DateTime.UtcNow > user.EmailVerificationTokenExpiresAt)
        {
            throw new ArgumentException(
                "Liên kết xác minh đã hết hạn. Vui lòng đăng nhập lại để nhận email mới.");
        }

        user.AccountStatus = AccountStatus.Active;
        user.EmailVerificationToken = null;
        user.EmailVerificationTokenExpiresAt = null;

        userRepo.Update(user);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return user;
    }

    private static async Task EnsureEmailAvailableAsync(
        IGenericRepository<User> userRepo,
        string email,
        CancellationToken cancellationToken)
    {
        var existing = await userRepo.FirstOrDefaultAsync(u => u.Email == email);
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

    private static async Task EnsurePhoneAvailableAsync(
        IGenericRepository<User> userRepo,
        string phone,
        CancellationToken cancellationToken)
    {
        var existing = await userRepo.FirstOrDefaultAsync(u => u.Phone == phone);
        if (existing is not null)
        {
            throw new InvalidOperationException("Số điện thoại đã được sử dụng.");
        }
    }

    private static async Task EnsureTaxCodeAvailableAsync(
        IGenericRepository<User> userRepo,
        string taxCode,
        CancellationToken cancellationToken)
    {
        var existing = await userRepo.FirstOrDefaultAsync(u => u.TaxCode == taxCode);
        if (existing is not null)
        {
            throw new InvalidOperationException("Số căn cước công dân đã được sử dụng.");
        }
    }

    private (string Token, DateTime ExpiresAt) CreateVerificationToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        var token = Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

        return (token, DateTime.UtcNow.AddMinutes(_verificationTokenExpiryMinutes));
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
