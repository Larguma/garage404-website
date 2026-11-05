using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace garage404_backend.Models;

public class ServiceType
{
  [BsonId]
  [BsonRepresentation(BsonType.ObjectId)]
  [JsonIgnore]
  public ObjectId Id { get; set; }

  public required int Type { get; set; } = 0;

  [Required, StringLength(100)]
  public required string Name { get; set; }

  [Required, StringLength(100)]
  [RegularExpression(
    "^[a-z0-9]+(?:-[a-z0-9]+)*$",
    ErrorMessage = "The slug must be lowercase words separated by dashes."
  )]
  public required string Slug { get; set; }
}
