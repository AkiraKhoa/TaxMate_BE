namespace TaxMate.Infrastructure.Options;

public class AppOptions
{
    public const string SectionName = "App";

    public string FrontendBaseUrl { get; set; } = "http://localhost:3000";
    public string ApiBaseUrl { get; set; } = "http://localhost:5086";
    public string VerificationPath { get; set; } = "/api/auth/verify-email";
    public int VerificationTokenExpiryMinutes { get; set; } = 1440;
}
