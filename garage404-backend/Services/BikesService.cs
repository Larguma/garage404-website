using garage404_backend.Models;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;

namespace garage404_backend.Services;

public class BikesService(IOptions<BackendDatabaseSettings> backendDatabaseSettings)
{
  private readonly IMongoCollection<Bike> _bikesCollection = new MongoClient(
    backendDatabaseSettings.Value.ConnectionString
  )
    .GetDatabase(backendDatabaseSettings.Value.DatabaseName)
    .GetCollection<Bike>(backendDatabaseSettings.Value.BikesCollectionName);

  public async Task<List<Bike>> GetAsync(CancellationToken cancellationToken = default) =>
    await _bikesCollection.Find(_ => true).ToListAsync(cancellationToken);

  public async Task<Bike?> GetAsync(ObjectId id, CancellationToken cancellationToken = default) =>
    await _bikesCollection.Find(x => x.Id == id).FirstOrDefaultAsync(cancellationToken);

  public async Task CreateAsync(Bike newBike, CancellationToken cancellationToken = default) =>
    await _bikesCollection.InsertOneAsync(newBike, cancellationToken: cancellationToken);

  public async Task<bool> UpdateAsync(Bike bike, CancellationToken cancellationToken = default)
  {
    var result = await _bikesCollection.ReplaceOneAsync(
      x => x.Id == bike.Id,
      bike,
      cancellationToken: cancellationToken
    );
    return result.MatchedCount > 0;
  }

  public async Task<bool> DeleteAsync(ObjectId id, CancellationToken cancellationToken = default)
  {
    var result = await _bikesCollection.DeleteOneAsync(x => x.Id == id, cancellationToken);
    return result.DeletedCount > 0;
  }

  public async Task<bool> AddImagesAsync(
    ObjectId id,
    IEnumerable<string> imageReferences,
    CancellationToken cancellationToken = default
  )
  {
    var result = await _bikesCollection.UpdateOneAsync(
      bike => bike.Id == id,
      Builders<Bike>.Update.PushEach(bike => bike.Images, imageReferences),
      cancellationToken: cancellationToken
    );

    return result.MatchedCount > 0;
  }

  public async Task<bool> RemoveImageAsync(
    ObjectId id,
    string imageReference,
    CancellationToken cancellationToken = default
  )
  {
    var result = await _bikesCollection.UpdateOneAsync(
      bike => bike.Id == id,
      Builders<Bike>.Update.Pull(bike => bike.Images, imageReference),
      cancellationToken: cancellationToken
    );

    return result.ModifiedCount > 0;
  }
}
