using System.Net.Http.Headers;
using Microsoft.Extensions.Options;
using TaxMate.Service.Interfaces;

namespace TaxMate.Infrastructure.Storage;

public class SupabaseStorageService : IFileStorageService
{
    private readonly HttpClient _httpClient;
    private readonly SupabaseStorageOptions _options;

    public SupabaseStorageService(
        HttpClient httpClient,
        IOptions<SupabaseStorageOptions> options)
    {
        _httpClient = httpClient;
        _options = options.Value;
    }

    public async Task<string> UploadAsync(
        Stream fileStream,
        string fileName,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        var storagePath =
            $"laws/{Guid.NewGuid()}_{fileName}";

        var uploadUrl =
            $"{_options.Url}/storage/v1/object/{_options.BucketName}/{storagePath}";

        using var content = new StreamContent(fileStream);

        content.Headers.ContentType =
            new MediaTypeHeaderValue(contentType);

        var request = new HttpRequestMessage(
            HttpMethod.Post,
            uploadUrl);

        request.Headers.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                _options.SecretKey);

        request.Headers.Add(
            "apikey",
            _options.SecretKey);

        request.Content = content;

        var response =
            await _httpClient.SendAsync(
                request,
                cancellationToken);

        response.EnsureSuccessStatusCode();

        return storagePath;
    }
}