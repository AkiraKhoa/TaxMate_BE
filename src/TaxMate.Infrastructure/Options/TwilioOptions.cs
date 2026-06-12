namespace TaxMate.Infrastructure.Options;

public class TwilioOptions
{
    public const string SectionName = "Twilio";

    public string AccountSid { get; set; } = string.Empty;
    public string AuthToken { get; set; } = string.Empty;
    public string FromPhoneNumber { get; set; } = string.Empty;

    /// <summary>
    /// Pre-registered alphanumeric sender for Vietnam (e.g. "TaxMate").
    /// When set, used instead of FromPhoneNumber for outbound SMS.
    /// </summary>
    public string? AlphanumericSenderId { get; set; }
}
