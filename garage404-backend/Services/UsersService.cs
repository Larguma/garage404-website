using garage404_backend.Models;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;

namespace garage404_backend.Services;

public class UsersService(IOptions<BackendDatabaseSettings> backendDatabaseSettings)
{
  private readonly IMongoCollection<User> _usersCollection = new MongoClient(backendDatabaseSettings.Value.ConnectionString)
        .GetDatabase(backendDatabaseSettings.Value.DatabaseName)
        .GetCollection<User>(backendDatabaseSettings.Value.UsersCollectionName);

  public async Task<List<User>> GetAsync() =>
        await _usersCollection.Find(_ => true).ToListAsync();

  public async Task<User?> GetAsync(ObjectId id) =>
      await _usersCollection.Find(x => x.Id == id).FirstOrDefaultAsync();

  public async Task<User?> GetAsync(string uid) =>
  await _usersCollection.Find(x => x.FirebaseUserId == uid).FirstOrDefaultAsync();

  public async Task<User?> GetByEmailAsync(string email) =>
      await _usersCollection.Find(x => x.Email == email).FirstOrDefaultAsync();

  public async Task CreateAsync(User newUser) =>
      await _usersCollection.InsertOneAsync(newUser);

  public async Task UpdateAsync(User user) =>
      await _usersCollection.ReplaceOneAsync(x => x.Id == user.Id, user);

  public async Task DeleteAsync(ObjectId id) =>
      await _usersCollection.DeleteOneAsync(x => x.Id == id);

}
