using garage404_backend.Models;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;

namespace garage404_backend.Services;

public class ServiceTypesService(IOptions<BackendDatabaseSettings> backendDatabaseSettings)
{
  private readonly IMongoCollection<ServiceType> _serviceTypesCollection = new MongoClient(
    backendDatabaseSettings.Value.ConnectionString
  )
    .GetDatabase(backendDatabaseSettings.Value.DatabaseName)
    .GetCollection<ServiceType>(backendDatabaseSettings.Value.ServiceTypesCollectionName);

  public async Task<List<ServiceType>> GetAsync(CancellationToken cancellationToken = default) =>
    await _serviceTypesCollection.Find(_ => true).SortBy(x => x.Type).ToListAsync(cancellationToken);

  public async Task<ServiceType?> GetAsync(ObjectId id, CancellationToken cancellationToken = default) =>
    await _serviceTypesCollection.Find(x => x.Id == id).FirstOrDefaultAsync(cancellationToken);

  public async Task<List<ServiceType>> GetAsync(int type, CancellationToken cancellationToken = default) =>
    await _serviceTypesCollection.Find(x => x.Type == type).ToListAsync(cancellationToken);

  /// <summary>
  /// Returns the display name of a service type, or <c>null</c> if the type does not exist.
  /// </summary>
  public async Task<string?> GetNameAsync(int type, CancellationToken cancellationToken = default) =>
    (await _serviceTypesCollection.Find(x => x.Type == type).FirstOrDefaultAsync(cancellationToken))?.Name;

  public async Task<bool> ExistsAsync(int type, CancellationToken cancellationToken = default) =>
    await _serviceTypesCollection.Find(x => x.Type == type).AnyAsync(cancellationToken);

  public async Task CreateAsync(ServiceType newServiceType, CancellationToken cancellationToken = default) =>
    await _serviceTypesCollection.InsertOneAsync(newServiceType, cancellationToken: cancellationToken);

  public async Task CreateAsync(List<ServiceType> newServiceTypes, CancellationToken cancellationToken = default) =>
    await _serviceTypesCollection.InsertManyAsync(newServiceTypes, cancellationToken: cancellationToken);

  public async Task UpdateAsync(ObjectId id, ServiceType updatedServiceType)
  {
    updatedServiceType.Id = id;
    await _serviceTypesCollection.ReplaceOneAsync(x => x.Id == id, updatedServiceType);
  }

  public async Task DeleteAsync(ObjectId id) => await _serviceTypesCollection.DeleteOneAsync(x => x.Id == id);

  public async Task<long> DeleteAsync(int type, CancellationToken cancellationToken = default) =>
    (await _serviceTypesCollection.DeleteManyAsync(x => x.Type == type, cancellationToken)).DeletedCount;
}
