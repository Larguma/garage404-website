using garage404_backend.Models.Utils;

namespace garage404_backend.Services;

public interface IDiscordWebhookService
{
  Task SendMessageAsync(string content);
  Task SendEmbedAsync(string? content, params DiscordEmbed[] embeds);
  Task<bool> SendMessageWithRetryAsync(string content, int maxRetries = 3);
}
