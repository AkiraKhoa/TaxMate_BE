namespace TaxMate.Infrastructure.Storage;

public class SupabaseStorageOptions
{
    public string Url { get; set; } = string.Empty;

    public string BucketName { get; set; } = string.Empty;

    public string SecretKey { get; set; } = string.Empty;
}