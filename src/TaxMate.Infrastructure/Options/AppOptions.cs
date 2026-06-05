namespace TaxMate.Infrastructure.Options;

public class AppOptions
{
    public const string SectionName = "App";

    public string FrontendBaseUrl { get; set; } = "http://localhost:3000";
    public string VerificationPath { get; set; } = "/verify-email";
}
