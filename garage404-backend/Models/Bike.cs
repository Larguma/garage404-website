using System.Text.Json.Serialization;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace garage404_backend.Models;

public class Bike
{
  [BsonId]
  [BsonRepresentation(BsonType.ObjectId)]
  [JsonIgnore]
  public ObjectId Id { get; set; }

  [BsonIgnore]
  [JsonPropertyName("id")]
  public string IdAsString => Id.ToString();

  public required string Make { get; set; } = string.Empty;
  public required string Model { get; set; } = string.Empty;
  public required DateTime Year { get; set; } = DateTime.MinValue;
  public required int Kilometers { get; set; } = 0;
  public required int EngineSize { get; set; } = 0;
  public required int KW { get; set; } = 0;
  public required DateTime LastMFKDate { get; set; } = DateTime.MinValue;
  public required int Price { get; set; } = 0;
  public List<string> Images { get; set; } = [];
}
