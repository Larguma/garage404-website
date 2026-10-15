using garage404_backend.Models;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;

namespace garage404_backend.Services;

public class ReservationsService(IOptions<BackendDatabaseSettings> backendDatabaseSettings)
{
  private readonly IMongoCollection<Reservation> _reservationsCollection = new MongoClient(backendDatabaseSettings.Value.ConnectionString)
        .GetDatabase(backendDatabaseSettings.Value.DatabaseName)
        .GetCollection<Reservation>(backendDatabaseSettings.Value.ReservationsCollectionName);

  public async Task<List<Reservation>> GetAsync() =>
        await _reservationsCollection.Find(_ => true).ToListAsync();

  public async Task<Reservation?> GetAsync(ObjectId id) =>
      await _reservationsCollection.Find(x => x.Id == id).FirstOrDefaultAsync();

  public async Task CreateAsync(Reservation newReservation) =>
      await _reservationsCollection.InsertOneAsync(newReservation);

  public async Task UpdateAsync(ObjectId id, Reservation updatedReservation) =>
      await _reservationsCollection.ReplaceOneAsync(x => x.Id == id, updatedReservation);

  public async Task DeleteAsync(ObjectId id) =>
      await _reservationsCollection.DeleteOneAsync(x => x.Id == id);

}
