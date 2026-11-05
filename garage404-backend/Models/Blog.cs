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

  [Required, StringLength(200)]
  public required string Title { get; set; } = string.Empty;

  [Required, StringLength(200)]
  [RegularExpression(
    "^[a-z0-9]+(?:-[a-z0-9]+)*$",
    ErrorMessage = "The slug must be lowercase words separated by dashes."
  )]
  public required string Slug { get; set; } = string.Empty;

  [Required]
  public required string Content { get; set; } = string.Empty;

  [StringLength(500)]
  public string? Excerpt { get; set; }

  public string? ImageUrl { get; set; }

  public DateTimeOffset? PublishedAt { get; set; }

  public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
