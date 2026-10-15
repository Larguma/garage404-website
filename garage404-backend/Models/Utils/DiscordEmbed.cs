using System;

namespace garage404_backend.Models.Utils;


public class DiscordEmbed
{
  public string? Title { get; set; }
  public string? Description { get; set; }
  public int? Color { get; set; }
  public List<EmbedField>? Fields { get; set; }
  public EmbedFooter? Footer { get; set; }
  public string? Timestamp { get; set; }
  public EmbedThumbnail? Thumbnail { get; set; }
  public EmbedImage? Image { get; set; }
}

public class EmbedField
{
  public string Name { get; set; } = string.Empty;
  public string Value { get; set; } = string.Empty;
  public bool Inline { get; set; }
}

public class EmbedFooter
{
  public string Text { get; set; } = string.Empty;
  public string? IconUrl { get; set; }
}

public class EmbedThumbnail
{
  public string Url { get; set; } = string.Empty;
}

public class EmbedImage
{
  public string Url { get; set; } = string.Empty;
}