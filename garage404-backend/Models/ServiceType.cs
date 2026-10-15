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

  public required string Name { get; set; }

  public required string Slug { get; set; }
}
