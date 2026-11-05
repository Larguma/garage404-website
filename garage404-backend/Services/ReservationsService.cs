using garage404_backend.Models;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;

namespace garage404_backend.Services;

public class ReservationsService(IOptions<BackendDatabaseSettings> backendDatabaseSettings)
{
  private readonly IMongoCollection<Reservation> _reservationsCollection = new MongoClient(
    backendDatabaseSettings.Value.ConnectionString
  )
    .GetDatabase(backendDatabaseSettings.Value.DatabaseName)
    .GetCollection<Reservation>(backendDatabaseSettings.Value.ReservationsCollectionName);

  public async Task<List<Reservation>> GetAsync(CancellationToken cancellationToken = default) =>
    await _reservationsCollection
      .Find(_ => true)
      .SortBy(reservation => reservation.PreferredDate)
      .ToListAsync(cancellationToken);

  public async Task<Reservation?> GetAsync(ObjectId id, CancellationToken cancellationToken = default) =>
    await _reservationsCollection.Find(x => x.Id == id).FirstOrDefaultAsync(cancellationToken);

  public async Task CreateAsync(Reservation newReservation, CancellationToken cancellationToken = default) =>
    await _reservationsCollection.InsertOneAsync(newReservation, cancellationToken: cancellationToken);

  public async Task<bool> UpdateAsync(
    ObjectId id,
    Reservation updatedReservation,
    CancellationToken cancellationToken = default
  )
  {
    updatedReservation.Id = id;
    var result = await _reservationsCollection.ReplaceOneAsync(
      x => x.Id == id,
      updatedReservation,
      cancellationToken: cancellationToken
    );
    return result.MatchedCount > 0;
  }

  public async Task<bool> DeleteAsync(ObjectId id, CancellationToken cancellationToken = default)
  {
    var result = await _reservationsCollection.DeleteOneAsync(x => x.Id == id, cancellationToken);
    return result.DeletedCount > 0;
  }
}
