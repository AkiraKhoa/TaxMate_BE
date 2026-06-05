using System.Security.Cryptography;
using Microsoft.Extensions.Logging;
using TaxMate.Model.Common;
using TaxMate.Model.DTO.Auth;
using TaxMate.Model.Entities;
using TaxMate.Repository.Interfaces;
using TaxMate.Service.Interfaces;

namespace TaxMate.Service.Services;

public class AuthService : IAuthService
{
    private const int VerificationTokenExpiryMinutes = 1440;

    private readonly IUnitOfWork _unitOfWork;
    private readonly IGoogleTokenValidator _googleTokenValidator;
    private readonly IJwtService _jwtService;
    private readonly IEmailService _emailService;
    private readonly ILogger<AuthService> _logger;

    public AuthService(
        IUnitOfWork unitOfWork,
        IGoogleTokenValidator googleTokenValidator,
        IJwtService jwtService,
        IEmailService emailService,
        ILogger<AuthService> logger)
    {
        _unitOfWork = unitOfWork;
        _googleTokenValidator = googleTokenValidator;
        _jwtService = jwtService;
        _emailService = emailService;
        _logger = logger;
    }

    public async Task<AuthResponse> LoginWithGoogleAsync(
        string idToken,
        CancellationToken cancellationToken = default)
    {
        var googleUser = await _googleTokenValidator.ValidateAsync(idToken, cancellationToken);
        var userRepo = _unitOfWork.Repository<User>();

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
            user!.GoogleId ??= googleUser.GoogleId;
            user.FullName = googleUser.FullName;
            user.AvatarUrl = googleUser.AvatarUrl;

            if (user.AccountStatus == AccountStatus.Pending
                && string.IsNullOrEmpty(user.EmailVerificationToken))
            {
                var (token, expiresAt) = CreateVerificationToken();
                user.EmailVerificationToken = token;
                user.EmailVerificationTokenExpiresAt = expiresAt;
                await TrySendVerificationEmailAsync(user, cancellationToken);
            }

            userRepo.Update(user);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
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
        var userRepo = _unitOfWork.Repository<User>();
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
        var user = await _unitOfWork.Repository<User>().GetByIdAsync(userId)
            ?? throw new KeyNotFoundException("Không tìm thấy người dùng.");

        return MapToDto(user);
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

        var userRepo = _unitOfWork.Repository<User>();
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

    private static (string Token, DateTime ExpiresAt) CreateVerificationToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        var token = Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

        return (token, DateTime.UtcNow.AddMinutes(VerificationTokenExpiryMinutes));
    }

    private static UserDto MapToDto(User user) => new()
    {
        Id = user.Id,
        Email = user.Email,
        FullName = user.FullName,
        AvatarUrl = user.AvatarUrl,
        AccountStatus = user.AccountStatus,
        Role = user.Role
    };
}
