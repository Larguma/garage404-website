using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace garage404_backend.Models;

public class Blog
{
  [BsonId]
  [BsonRepresentation(BsonType.ObjectId)]
  [JsonIgnore]
  public ObjectId Id { get; set; }

  [BsonIgnore]
  [JsonPropertyName("id")]
  public string IdAsString => Id.ToString();

  [Required]
  public required string Title { get; set; } = string.Empty;

  [Required]
  public required string Slug { get; set; } = string.Empty;

  [Required]
  public required string Content { get; set; } = string.Empty;

  public string? Excerpt { get; set; }

  public string? ImageUrl { get; set; }

  public DateTimeOffset? PublishedAt { get; set; }

  public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
