using System;
using garage404_backend.Models;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;

namespace garage404_backend.Services;

public class BikesService(IOptions<BackendDatabaseSettings> backendDatabaseSettings)
{
  private readonly IMongoCollection<Bike> _bikesCollection = new MongoClient(backendDatabaseSettings.Value.ConnectionString)
        .GetDatabase(backendDatabaseSettings.Value.DatabaseName)
        .GetCollection<Bike>(backendDatabaseSettings.Value.BikesCollectionName);

  public async Task<List<Bike>> GetAsync(CancellationToken cancellationToken = default) =>
        await _bikesCollection.Find(_ => true).ToListAsync(cancellationToken);

  public async Task<Bike?> GetAsync(ObjectId id, CancellationToken cancellationToken = default) =>
      await _bikesCollection.Find(x => x.Id == id).FirstOrDefaultAsync(cancellationToken);

  public async Task CreateAsync(Bike newBike) =>
      await _bikesCollection.InsertOneAsync(newBike);

  public async Task UpdateAsync(Bike bike) =>
      await _bikesCollection.ReplaceOneAsync(x => x.Id == bike.Id, bike);

  public async Task DeleteAsync(ObjectId id) =>
      await _bikesCollection.DeleteOneAsync(x => x.Id == id);

  public async Task<bool> AddImagesAsync(
      ObjectId id,
      IEnumerable<string> imageReferences,
      CancellationToken cancellationToken = default)
  {
    var result = await _bikesCollection.UpdateOneAsync(
        bike => bike.Id == id,
        Builders<Bike>.Update.PushEach(bike => bike.Images, imageReferences),
        cancellationToken: cancellationToken);

    return result.MatchedCount > 0;
  }
}
