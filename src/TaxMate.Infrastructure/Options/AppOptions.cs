namespace TaxMate.Infrastructure.Options;

public class AppOptions
{
    public const string SectionName = "App";

    public string FrontendBaseUrl { get; set; } = "https://localhost:5173";
    public string ApiBaseUrl { get; set; } = "http://localhost:5086";
    /// <summary>
    /// Base URL used in verification emails. Must be reachable from a desktop browser
    /// (use localhost, not the Android emulator alias 10.0.2.2).
    /// </summary>
    public string VerificationLinkBaseUrl { get; set; } = "http://localhost:5086";
    public string VerificationPath { get; set; } = "/api/auth/verify-email";
    public int VerificationTokenExpiryMinutes { get; set; } = 1440;
    public string MobileVerificationSuccessUrl { get; set; } = "taxmatemobile://auth/verify-success";
}
