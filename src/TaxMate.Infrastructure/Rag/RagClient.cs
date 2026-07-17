using System.Net.Http.Json;
using Microsoft.Extensions.Options;
using TaxMate.Model.DTO.Rag;
using TaxMate.Service.Exceptions;
using TaxMate.Service.Interfaces;

namespace TaxMate.Infrastructure.Rag;

public class RagClient : IRagClient
{
    private readonly HttpClient _httpClient;
    private readonly RagApiOptions _options;

    public RagClient(
        HttpClient httpClient,
        IOptions<RagApiOptions> options)
    {
        _httpClient = httpClient;
        _options = options.Value;
    }

    public async Task<RagAskResponse> AskAsync(
        RagAskRequest request,
        CancellationToken cancellationToken = default)
    {
        HttpResponseMessage response;

        try
        {
            response = await _httpClient.PostAsJsonAsync(
                _options.AskEndpoint,
                request,
                cancellationToken);
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new Exception(
                "RAG service did not respond within the allowed time.");
        }
        catch (HttpRequestException exception)
        {
            throw new Exception(
                "Cannot connect to RAG service.",
                exception);
        }

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(
                cancellationToken);

            throw new Exception(
                $"RAG service returned status {(int)response.StatusCode}: {errorBody}");
        }

        var result = await response.Content
            .ReadFromJsonAsync<RagAskResponse>(
                cancellationToken: cancellationToken);

        if (result is null)
        {
            throw new Exception(
                "RAG service returned an invalid response.");
        }

        return result;
    }
}