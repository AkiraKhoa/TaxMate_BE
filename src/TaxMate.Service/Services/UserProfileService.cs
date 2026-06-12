using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using TaxMate.Model.DTO.Auth;
using TaxMate.Model.Entities;
using TaxMate.Repository.Interfaces;
using TaxMate.Service.Common;
using TaxMate.Service.Exceptions;
using TaxMate.Service.Interfaces;

namespace TaxMate.Service.Services;

public partial class UserProfileService : IUserProfileService
{
    private const int OtpExpiryMinutes = 5;
    private const int ResendCooldownSeconds = 15;

    private readonly IUnitOfWork _unitOfWork;
    private readonly IGenericRepository<User> _users;
    private readonly IEmailService _emailService;
    private readonly IMemoryCache _cache;
    private readonly ILogger<UserProfileService> _logger;

    public UserProfileService(
        IUnitOfWork unitOfWork,
        IGenericRepository<User> users,
        IEmailService emailService,
        IMemoryCache cache,
        ILogger<UserProfileService> logger)
    {
        _unitOfWork = unitOfWork;
        _users = users;
        _emailService = emailService;
        _cache = cache;
        _logger = logger;
    }

    public async Task<ProfileOtpResponse> InitiateProfileUpdateAsync(
        Guid userId,
        string taxCode,
        string phone,
        CancellationToken cancellationToken = default)
    {
        UserInputValidator.ValidateTaxCode(taxCode);
        UserInputValidator.ValidatePhone(phone);

        var userRepo = _users;
        var user = await userRepo.GetByIdAsync(userId)
            ?? throw new KeyNotFoundException("Không tìm thấy người dùng.");

        EnsureProfileNotYetSet(user);
        await EnsureTaxCodeIsUniqueAsync(userRepo, taxCode, userId, cancellationToken);

        var otp = GenerateOtp();
        var now = DateTime.UtcNow;
        var expiresAt = now.AddMinutes(OtpExpiryMinutes);

        var pending = new PendingProfileOtp(
            taxCode,
            phone,
            HashOtp(otp),
            expiresAt,
            now);

        StorePendingOtp(userId, pending);

        await SendOtpEmailAsync(user, otp, cancellationToken);

        return BuildOtpResponse(user.Email, expiresAt, now);
    }

    public async Task<VerifyProfileEmailResponse> VerifyAndUpdateProfileAsync(
        Guid userId,
        string otp,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(otp) || !OtpRegex().IsMatch(otp))
        {
            throw new ArgumentException("Mã OTP phải gồm 6 chữ số.");
        }

        if (!_cache.TryGetValue(GetCacheKey(userId), out PendingProfileOtp? pending)
            || pending is null)
        {
            throw new InvalidOperationException("Không có phiên xác minh email đang hoạt động. Vui lòng gửi lại thông tin.");
        }

        if (DateTime.UtcNow > pending.ExpiresAt)
        {
            _cache.Remove(GetCacheKey(userId));
            throw new ArgumentException("Mã OTP đã hết hạn. Vui lòng gửi lại thông tin.");
        }

        if (!string.Equals(pending.OtpHash, HashOtp(otp), StringComparison.Ordinal))
        {
            throw new ArgumentException("Mã OTP không đúng.");
        }

        var userRepo = _users;
        var user = await userRepo.GetByIdAsync(userId)
            ?? throw new KeyNotFoundException("Không tìm thấy người dùng.");

        EnsureProfileNotYetSet(user);
        await EnsureTaxCodeIsUniqueAsync(userRepo, pending.TaxCode, userId, cancellationToken);

        user.TaxCode = pending.TaxCode;
        user.Phone = pending.Phone;

        userRepo.Update(user);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _cache.Remove(GetCacheKey(userId));

        return new VerifyProfileEmailResponse
        {
            Message = "Cập nhật thông tin thành công.",
            User = MapToDto(user)
        };
    }

    public async Task<ProfileOtpResponse> ResendProfileOtpAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        if (!_cache.TryGetValue(GetCacheKey(userId), out PendingProfileOtp? pending)
            || pending is null)
        {
            throw new InvalidOperationException("Không có phiên xác minh email đang hoạt động. Vui lòng gửi lại thông tin.");
        }

        var userRepo = _users;
        var user = await userRepo.GetByIdAsync(userId)
            ?? throw new KeyNotFoundException("Không tìm thấy người dùng.");

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
            LastSentAt = now
        };

        StorePendingOtp(userId, updated);

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
            await _emailService.SendProfileOtpEmailAsync(
                user.Email,
                user.FullName,
                otp,
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send profile OTP email to {Email}", user.Email);
            throw new InvalidOperationException("Không thể gửi email. Vui lòng thử lại sau.", ex);
        }
    }

    private void StorePendingOtp(Guid userId, PendingProfileOtp pending)
    {
        _cache.Set(
            GetCacheKey(userId),
            pending,
            new MemoryCacheEntryOptions
            {
                AbsoluteExpiration = pending.ExpiresAt
            });
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

    private static void EnsureProfileNotYetSet(User user)
    {
        if (!string.IsNullOrWhiteSpace(user.TaxCode) || !string.IsNullOrWhiteSpace(user.Phone))
        {
            throw new InvalidOperationException("Thông tin căn cước và số điện thoại đã được cập nhật.");
        }
    }

    private static async Task EnsureTaxCodeIsUniqueAsync(
        IGenericRepository<User> userRepo,
        string taxCode,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var existing = await userRepo.FirstOrDefaultAsync(u => u.TaxCode == taxCode && u.Id != userId);
        if (existing is not null)
        {
            throw new InvalidOperationException("Số căn cước công dân đã được sử dụng.");
        }
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

    private static string GetCacheKey(Guid userId) => $"profile-otp:{userId}";

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

    private sealed record PendingProfileOtp(
        string TaxCode,
        string Phone,
        string OtpHash,
        DateTime ExpiresAt,
        DateTime LastSentAt);

    [GeneratedRegex(@"^\d{6}$")]
    private static partial Regex OtpRegex();
}
