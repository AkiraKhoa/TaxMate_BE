namespace TaxMate.Service.Exceptions;

public class ResendCooldownException : Exception
{
    public ResendCooldownException(int retryAfterSeconds)
        : base($"Vui lòng đợi {retryAfterSeconds} giây trước khi gửi lại mã OTP.")
    {
        RetryAfterSeconds = retryAfterSeconds;
    }

    public int RetryAfterSeconds { get; }
}
