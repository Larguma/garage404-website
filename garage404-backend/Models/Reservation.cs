using System.ComponentModel.DataAnnotations;
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

  [Required, StringLength(100)]
  public required string Name { get; set; } = "John Doe";

  [Required, EmailAddress, StringLength(254)]
  public required string Email { get; set; } = "john.doe@example.com";

  [Required, StringLength(30)]
  [RegularExpression(@"^\+?[0-9 ()./-]{6,30}$", ErrorMessage = "The phone number is invalid.")]
  public required string Phone { get; set; } = "079 123 45 67";

  public required DateTime PreferredDate { get; set; } = DateTime.MinValue;

  /// <summary>
  /// Set by the backend when an admin accepts the reservation.
  /// </summary>
  public DateTime? ActualDate { get; set; }

  /// <summary>
  /// Google Calendar event id, set by the backend when the reservation is accepted.
  /// </summary>
  public string? CalEventId { get; set; }

  public required int ServiceType { get; set; }

  [Required, StringLength(100)]
  public required string Vehicle { get; set; } = "RE Himalayan 450";

  [StringLength(2000)]
  public string? Details { get; set; }
}
