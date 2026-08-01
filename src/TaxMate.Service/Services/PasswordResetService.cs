using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using TaxMate.Model.Common;
using TaxMate.Model.DTO.Auth;
using TaxMate.Model.Entities;
using TaxMate.Repository.Interfaces;
using TaxMate.Service.Common;
using TaxMate.Service.Exceptions;
using TaxMate.Service.Interfaces;

namespace TaxMate.Service.Services;

public partial class PasswordResetService : IPasswordResetService
{
    private const int OtpExpiryMinutes = 5;
    private const int VerifiedSessionMinutes = 10;
    private const int ResendCooldownSeconds = 15;

    private readonly IUnitOfWork _unitOfWork;
    private readonly IGenericRepository<User> _users;
    private readonly IEmailService _emailService;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IMemoryCache _cache;
    private readonly ILogger<PasswordResetService> _logger;

    public PasswordResetService(
        IUnitOfWork unitOfWork,
        IGenericRepository<User> users,
        IEmailService emailService,
        IPasswordHasher passwordHasher,
        IMemoryCache cache,
        ILogger<PasswordResetService> logger)
    {
        _unitOfWork = unitOfWork;
        _users = users;
        _emailService = emailService;
        _passwordHasher = passwordHasher;
        _cache = cache;
        _logger = logger;
    }

    public async Task<ProfileOtpResponse> InitiateResetAsync(
        string email,
        CancellationToken cancellationToken = default)
    {
        UserInputValidator.ValidateEmail(email);
        var normalizedEmail = email.Trim().ToLowerInvariant();

        var user = await _users.FirstOrDefaultAsync(u => u.Email == normalizedEmail);
        if (user is null)
        {
            return BuildGenericOtpResponse(normalizedEmail);
        }

        EnsurePasswordResetAllowed(user);

        var otp = GenerateOtp();
        var now = DateTime.UtcNow;
        var expiresAt = now.AddMinutes(OtpExpiryMinutes);

        var pending = new PendingPasswordResetOtp(
            HashOtp(otp),
            expiresAt,
            now);

        StorePendingOtp(normalizedEmail, pending);

        await SendOtpEmailAsync(user, otp, cancellationToken);

        return BuildOtpResponse(user.Email, expiresAt, now);
    }

    public Task<VerifyResetPasswordOtpResponse> VerifyResetOtpAsync(
        string email,
        string otp,
        CancellationToken cancellationToken = default)
    {
        UserInputValidator.ValidateEmail(email);
        var normalizedEmail = email.Trim().ToLowerInvariant();

        if (string.IsNullOrWhiteSpace(otp) || !OtpRegex().IsMatch(otp))
        {
            throw new ArgumentException("Mã OTP phải gồm 6 chữ số.");
        }

        if (!_cache.TryGetValue(GetCacheKey(normalizedEmail), out PendingPasswordResetOtp? pending)
            || pending is null)
        {
            throw new InvalidOperationException("Không có phiên đặt lại mật khẩu đang hoạt động. Vui lòng yêu cầu mã OTP mới.");
        }

        if (DateTime.UtcNow > pending.ExpiresAt)
        {
            _cache.Remove(GetCacheKey(normalizedEmail));
            throw new ArgumentException("Mã OTP đã hết hạn. Vui lòng yêu cầu mã OTP mới.");
        }

        if (!string.Equals(pending.OtpHash, HashOtp(otp), StringComparison.Ordinal))
        {
            throw new ArgumentException("Mã OTP không đúng.");
        }

        var now = DateTime.UtcNow;
        var verifiedExpiresAt = now.AddMinutes(VerifiedSessionMinutes);

        var verified = pending with
        {
            OtpVerified = true,
            VerifiedExpiresAt = verifiedExpiresAt
        };

        StorePendingOtp(normalizedEmail, verified);

        return Task.FromResult(new VerifyResetPasswordOtpResponse
        {
            Message = "Xác minh OTP thành công. Vui lòng nhập mật khẩu mới."
        });
    }

    public async Task<string> ResetPasswordAsync(
        string email,
        string newPassword,
        string confirmPassword,
        CancellationToken cancellationToken = default)
    {
        UserInputValidator.ValidateEmail(email);
        var normalizedEmail = email.Trim().ToLowerInvariant();

        UserInputValidator.ValidatePassword(newPassword);

        if (!string.Equals(newPassword, confirmPassword, StringComparison.Ordinal))
        {
            throw new ArgumentException("Mật khẩu xác nhận không khớp.");
        }

        if (!_cache.TryGetValue(GetCacheKey(normalizedEmail), out PendingPasswordResetOtp? pending)
            || pending is null)
        {
            throw new InvalidOperationException("Không có phiên đặt lại mật khẩu đang hoạt động. Vui lòng yêu cầu mã OTP mới.");
        }

        if (!pending.OtpVerified || pending.VerifiedExpiresAt is null)
        {
            throw new InvalidOperationException("Vui lòng xác minh OTP trước khi đặt lại mật khẩu.");
        }

        if (DateTime.UtcNow > pending.VerifiedExpiresAt)
        {
            _cache.Remove(GetCacheKey(normalizedEmail));
            throw new ArgumentException("Phiên đặt lại mật khẩu đã hết hạn. Vui lòng yêu cầu mã OTP mới.");
        }

        var user = await _users.FirstOrDefaultAsync(u => u.Email == normalizedEmail)
            ?? throw new KeyNotFoundException("Không tìm thấy người dùng.");

        EnsurePasswordResetAllowed(user);

        user.PasswordHash = _passwordHasher.Hash(newPassword);

        _users.Update(user);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _cache.Remove(GetCacheKey(normalizedEmail));

        return "Đặt lại mật khẩu thành công.";
    }

    public async Task<ProfileOtpResponse> ResendResetOtpAsync(
        string email,
        CancellationToken cancellationToken = default)
    {
        UserInputValidator.ValidateEmail(email);
        var normalizedEmail = email.Trim().ToLowerInvariant();

        if (!_cache.TryGetValue(GetCacheKey(normalizedEmail), out PendingPasswordResetOtp? pending)
            || pending is null)
        {
            throw new InvalidOperationException("Không có phiên đặt lại mật khẩu đang hoạt động. Vui lòng yêu cầu mã OTP mới.");
        }

        var user = await _users.FirstOrDefaultAsync(u => u.Email == normalizedEmail)
            ?? throw new KeyNotFoundException("Không tìm thấy người dùng.");

        EnsurePasswordResetAllowed(user);

        var now = DateTime.UtcNow;
        var elapsed = (int)(now - pending.LastSentAt).TotalSeconds;
        if (elapsed < ResendCooldownSeconds)
        {
            throw new ResendCooldownException(ResendCooldownSeconds - elapsed);
        }

        var otp = GenerateOtp();
        var expiresAt = now.AddMinutes(OtpExpiryMinutes);

        var updated = pending with
        {
            OtpHash = HashOtp(otp),
            ExpiresAt = expiresAt,
            LastSentAt = now,
            OtpVerified = false,
            VerifiedExpiresAt = null
        };

        StorePendingOtp(normalizedEmail, updated);

        await SendOtpEmailAsync(user, otp, cancellationToken);

        return BuildOtpResponse(user.Email, expiresAt, now);
    }

    private async Task SendOtpEmailAsync(
        User user,
        string otp,
        CancellationToken cancellationToken)
    {
        try
        {
            await _emailService.SendPasswordResetOtpEmailAsync(
                user.Email,
                user.FullName,
                otp,
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send password reset OTP email to {Email}", user.Email);
            throw new InvalidOperationException("Không thể gửi email. Vui lòng thử lại sau.", ex);
        }
    }

    private void StorePendingOtp(string normalizedEmail, PendingPasswordResetOtp pending)
    {
        var expiration = pending.OtpVerified && pending.VerifiedExpiresAt is not null
            ? pending.VerifiedExpiresAt.Value
            : pending.ExpiresAt;

        _cache.Set(
            GetCacheKey(normalizedEmail),
            pending,
            new MemoryCacheEntryOptions
            {
                AbsoluteExpiration = expiration
            });
    }

    private static void EnsurePasswordResetAllowed(User user)
    {
        if (string.IsNullOrWhiteSpace(user.PasswordHash))
        {
            throw new InvalidOperationException("Tài khoản đăng nhập bằng Google. Vui lòng sử dụng Google để đăng nhập.");
        }

        if (user.AccountStatus != AccountStatus.Active)
        {
            throw new InvalidOperationException("Tài khoản chưa được kích hoạt. Vui lòng xác minh email trước.");
        }
    }

    private static ProfileOtpResponse BuildOtpResponse(
        string email,
        DateTime expiresAt,
        DateTime lastSentAt) =>
        new()
        {
            Message = $"Mã OTP đã được gửi đến email {email}.",
            ExpiresAt = expiresAt,
            ResendAvailableAt = lastSentAt.AddSeconds(ResendCooldownSeconds)
        };

    private static ProfileOtpResponse BuildGenericOtpResponse(string email)
    {
        var now = DateTime.UtcNow;
        return new ProfileOtpResponse
        {
            Message = $"Nếu email {email} tồn tại trong hệ thống, mã OTP sẽ được gửi đến hộp thư của bạn.",
            ExpiresAt = now.AddMinutes(OtpExpiryMinutes),
            ResendAvailableAt = now.AddSeconds(ResendCooldownSeconds)
        };
    }

    private static string GenerateOtp()
    {
        var value = RandomNumberGenerator.GetInt32(0, 1_000_000);
        return value.ToString("D6");
    }

    private static string HashOtp(string otp)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(otp));
        return Convert.ToHexString(bytes);
    }

    private static string GetCacheKey(string normalizedEmail) => $"password-reset-otp:{normalizedEmail}";

    private sealed record PendingPasswordResetOtp(
        string OtpHash,
        DateTime ExpiresAt,
        DateTime LastSentAt,
        bool OtpVerified = false,
        DateTime? VerifiedExpiresAt = null);

    [GeneratedRegex(@"^\d{6}$")]
    private static partial Regex OtpRegex();
}
