using garage404_backend.Models;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;

namespace garage404_backend.Services;

public class ServiceTypesService(IOptions<BackendDatabaseSettings> backendDatabaseSettings)
{
  private readonly IMongoCollection<ServiceType> _serviceTypesCollection = new MongoClient(backendDatabaseSettings.Value.ConnectionString)
         .GetDatabase(backendDatabaseSettings.Value.DatabaseName)
         .GetCollection<ServiceType>(backendDatabaseSettings.Value.ServiceTypesCollectionName);

  public async Task<List<ServiceType>> GetAsync() =>
        await _serviceTypesCollection.Find(_ => true).ToListAsync();

  public async Task<ServiceType?> GetAsync(ObjectId id) =>
      await _serviceTypesCollection.Find(x => x.Id == id).FirstOrDefaultAsync();

  public async Task<List<ServiceType>> GetAsync(int type) =>
      await _serviceTypesCollection.Find(x => x.Type == type).ToListAsync();

  public async Task CreateAsync(ServiceType newServiceType) =>
      await _serviceTypesCollection.InsertOneAsync(newServiceType);

  public async Task CreateAsync(List<ServiceType> newServiceTypes) =>
      await _serviceTypesCollection.InsertManyAsync(newServiceTypes);

  public async Task UpdateAsync(ObjectId id, ServiceType updatedServiceType) =>
      await _serviceTypesCollection.ReplaceOneAsync(x => x.Id == id, updatedServiceType);

  public async Task DeleteAsync(ObjectId id) =>
      await _serviceTypesCollection.DeleteOneAsync(x => x.Id == id);
  
  public async Task DeleteAsync(int type) =>
      await _serviceTypesCollection.DeleteManyAsync(x => x.Type == type);
}
  