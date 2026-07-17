using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using TaxMate.Service.Interfaces;

namespace TaxMate.Service.Services;

public class NotificationService : INotificationService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<NotificationService> _logger;

    public NotificationService(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<NotificationService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task SendTelegramAsync(string chatId, string message, CancellationToken cancellationToken = default)
    {
        var token = _configuration["Notification:Telegram:BotToken"];
        if (string.IsNullOrEmpty(token))
        {
            _logger.LogWarning("Telegram BotToken is not configured. Skipping Telegram notification.");
            return;
        }

        try
        {
            var client = _httpClientFactory.CreateClient();
            var url = $"https://api.telegram.org/bot{token}/sendMessage";
            var payload = new
            {
                chat_id = chatId,
                text = message,
                parse_mode = "Markdown"
            };

            var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
            var response = await client.PostAsync(url, content, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError("Failed to send Telegram message. Status: {Status}, Error: {Error}", response.StatusCode, errorContent);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred while sending Telegram notification.");
        }
    }

    public async Task SendFcmPushAsync(string deviceToken, string title, string body, CancellationToken cancellationToken = default)
    {
        var projectId = _configuration["Notification:Fcm:ProjectId"];
        if (string.IsNullOrEmpty(projectId))
        {
            _logger.LogWarning("FCM ProjectId is not configured. Skipping FCM notification.");
            return;
        }

        try
        {
            var client = _httpClientFactory.CreateClient();
            var url = $"https://fcm.googleapis.com/v1/projects/{projectId}/messages:send";

            // Lấy token xác thực Google. Trong thực tế sẽ dùng GoogleCredential để lấy token.
            // Dưới đây giả lập lấy token từ config hoặc mock token để phục vụ chạy thử.
            var oauthToken = _configuration["Notification:Fcm:OAuthToken"] ?? "MOCK_FCM_BEARER_TOKEN";

            var payload = new
            {
                message = new
                {
                    token = deviceToken,
                    notification = new
                    {
                        title = title,
                        body = body
                    },
                    data = new
                    {
                        click_action = "FLUTTER_NOTIFICATION_CLICK"
                    }
                }
            };

            var request = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", oauthToken);

            var response = await client.SendAsync(request, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError("Failed to send FCM notification. Status: {Status}, Error: {Error}", response.StatusCode, errorContent);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred while sending FCM notification.");
        }
    }
}
