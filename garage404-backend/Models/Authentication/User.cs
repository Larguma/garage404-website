using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace garage404_backend.Models;

public class User
{
  [BsonId]
  [BsonRepresentation(BsonType.ObjectId)]
  [JsonIgnore]
  public ObjectId Id { get; set; }

  [Required]
  public required string Name { get; set; }

  public string? Email { get; set; }

  public string FirebaseUserId { get; set; } = string.Empty;

  public bool IsAdmin { get; set; } = false;
}
