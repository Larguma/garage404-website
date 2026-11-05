using System.Text;
using System.Text.Json;
using garage404_backend.Interfaces;
using garage404_backend.Models.Utils;

namespace garage404_backend.Services.Utils;

public class DiscordWebhookService(
  HttpClient httpClient,
  IConfiguration configuration,
  ILogger<DiscordWebhookService> logger
) : IDiscordWebhookService
{
  private readonly HttpClient _httpClient = httpClient;
  private readonly string? _webhookUrl = configuration["Discord:WebhookUrl"];
  private readonly ILogger<DiscordWebhookService> _logger = logger;
  private static readonly JsonSerializerOptions JsonOptions = new()
  {
    PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
  };

  // Messages contain user-provided text: never let it ping @everyone, roles or users.
  private static readonly object NoMentions = new { Parse = Array.Empty<string>() };

  public async Task SendEmbedAsync(string? content, params DiscordEmbed[] embeds)
  {
    try
    {
      await PostAsync(
        new
        {
          content,
          embeds,
          AllowedMentions = NoMentions,
        }
      );
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
      await PostAsync(new { content, AllowedMentions = NoMentions });
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
        await PostAsync(new { content, AllowedMentions = NoMentions });
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

  private async Task PostAsync(object payload)
  {
    if (string.IsNullOrWhiteSpace(_webhookUrl))
    {
      throw new InvalidOperationException("Discord:WebhookUrl is not configured.");
    }

    var json = JsonSerializer.Serialize(payload, JsonOptions);
    using var httpContent = new StringContent(json, Encoding.UTF8, "application/json");
    using var response = await _httpClient.PostAsync(_webhookUrl, httpContent);
    response.EnsureSuccessStatusCode();
  }
}
