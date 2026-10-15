using System;
using System.Text.Json.Serialization;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace garage404_backend.Models;

public class Reservation
{
  [BsonId]
  [BsonRepresentation(BsonType.ObjectId)]
  [JsonIgnore]
  public ObjectId Id { get; set; }

  [BsonIgnore]
  [JsonPropertyName("id")]
  public string IdAsString => Id.ToString();

  public required string Name { get; set; } = "John Doe";
  public required string Email { get; set; } = "john.doe@example.com";
  public required string Phone { get; set; } = "079 123 45 67";
  public required DateTime PrefferedDate { get; set; }
  public DateTime? ActualDate { get; set; }
  public string? CalEventId { get; set; }
  public required int ServiceType { get; set; }
  public required string Vehicle { get; set; } = "RE Himmy 450";
  public string? Details { get; set; }
}
