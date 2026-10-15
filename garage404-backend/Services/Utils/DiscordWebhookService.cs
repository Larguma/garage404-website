using System.Text;
using System.Text.Json;
using garage404_backend.Models.Utils;

namespace garage404_backend.Services;

public class DiscordWebhookService(HttpClient httpClient, IConfiguration configuration, ILogger<DiscordWebhookService> logger) : IDiscordWebhookService
{
  private readonly HttpClient _httpClient = httpClient;
  private readonly string _webhookUrl = configuration["Discord:WebhookUrl"] ?? throw new ArgumentNullException("Discord webhook URL not configured");
  private readonly ILogger<DiscordWebhookService> _logger = logger;
  private static readonly JsonSerializerOptions JsonOptions = new()
  {
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
  };

  public async Task SendEmbedAsync(string? content, params DiscordEmbed[] embeds)
  {
    try
    {
      var payload = new { content, embeds };
      var json = JsonSerializer.Serialize(payload, JsonOptions);

      var httpContent = new StringContent(json, Encoding.UTF8, "application/json");

      var response = await _httpClient.PostAsync(_webhookUrl, httpContent);
      response.EnsureSuccessStatusCode();

      _logger.LogInformation("Discord embed sent successfully");
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Failed to send Discord embed");
      throw;
    }
  }

  public async Task SendMessageAsync(string content)
  {
    try
    {
      var payload = new { content };
      var json = JsonSerializer.Serialize(payload);
      var httpContent = new StringContent(json, Encoding.UTF8, "application/json");

      var response = await _httpClient.PostAsync(_webhookUrl, httpContent);
      response.EnsureSuccessStatusCode();

      _logger.LogInformation("Discord message sent successfully");
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Failed to send Discord message");
      throw;
    }
  }

  public async Task<bool> SendMessageWithRetryAsync(string content, int maxRetries = 3)
  {
    for (int attempt = 0; attempt < maxRetries; attempt++)
    {
      try
      {
        var payload = new { content };
        var json = JsonSerializer.Serialize(payload);
        var httpContent = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await _httpClient.PostAsync(_webhookUrl, httpContent);
        response.EnsureSuccessStatusCode();

        _logger.LogInformation("Discord message sent successfully");
        return true;
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Failed to send Discord message on attempt {Attempt}", attempt + 1);
      }
    }

    _logger.LogError("Failed to send Discord message after {MaxRetries} attempts", maxRetries);
    return false;
  }
}
