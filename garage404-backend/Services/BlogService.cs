using garage404_backend.Models;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;

namespace garage404_backend.Services;

public class BlogService(IOptions<BackendDatabaseSettings> backendDatabaseSettings)
{
  private readonly IMongoCollection<Blog> _blogCollection = new MongoClient(
    backendDatabaseSettings.Value.ConnectionString
  )
    .GetDatabase(backendDatabaseSettings.Value.DatabaseName)
    .GetCollection<Blog>(backendDatabaseSettings.Value.BlogCollectionName);

  public async Task<List<Blog>> GetAsync(CancellationToken cancellationToken = default) =>
    await _blogCollection
      .Find(_ => true)
      .SortByDescending(article => article.PublishedAt)
      .ToListAsync(cancellationToken);

  public async Task<Blog?> GetAsync(ObjectId id, CancellationToken cancellationToken = default) =>
    await _blogCollection.Find(article => article.Id == id).FirstOrDefaultAsync(cancellationToken);

  public async Task CreateAsync(Blog article, CancellationToken cancellationToken = default) =>
    await _blogCollection.InsertOneAsync(article, cancellationToken: cancellationToken);

  public async Task<bool> UpdateAsync(ObjectId id, Blog article, CancellationToken cancellationToken = default)
  {
    article.Id = id;
    article.UpdatedAt = DateTimeOffset.UtcNow;
    var result = await _blogCollection.ReplaceOneAsync(
      existingArticle => existingArticle.Id == id,
      article,
      cancellationToken: cancellationToken
    );

    return result.MatchedCount > 0;
  }

  internal async Task<bool> DeleteAsync(ObjectId id, CancellationToken cancellationToken)
  {
    var result = await _blogCollection.DeleteOneAsync(
      article => article.Id == id,
      cancellationToken: cancellationToken
    );

    return result.DeletedCount > 0;
  }
}
