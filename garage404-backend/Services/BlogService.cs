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

  /// <summary>Returns the articles, newest first. Drafts and scheduled articles are only included on request.</summary>
  public async Task<List<Blog>> GetAsync(bool includeUnpublished, CancellationToken cancellationToken = default)
  {
    var now = DateTimeOffset.UtcNow;
    var filter = includeUnpublished
      ? Builders<Blog>.Filter.Empty
      : Builders<Blog>.Filter.Lte(article => article.PublishedAt, now);

    return await _blogCollection
      .Find(filter)
      .SortByDescending(article => article.PublishedAt)
      .ToListAsync(cancellationToken);
  }

  public async Task<Blog?> GetAsync(ObjectId id, CancellationToken cancellationToken = default) =>
    await _blogCollection.Find(article => article.Id == id).FirstOrDefaultAsync(cancellationToken);

  public async Task<Blog?> GetBySlugAsync(string slug, CancellationToken cancellationToken = default) =>
    await _blogCollection.Find(article => article.Slug == slug).FirstOrDefaultAsync(cancellationToken);

  public async Task<bool> SlugExistsAsync(
    string slug,
    ObjectId? excludedId = null,
    CancellationToken cancellationToken = default
  )
  {
    var filter = Builders<Blog>.Filter.Eq(article => article.Slug, slug);
    if (excludedId is ObjectId id)
    {
      filter &= Builders<Blog>.Filter.Ne(article => article.Id, id);
    }

    return await _blogCollection.Find(filter).AnyAsync(cancellationToken);
  }

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

  public async Task<bool> DeleteAsync(ObjectId id, CancellationToken cancellationToken = default)
  {
    var result = await _blogCollection.DeleteOneAsync(
      article => article.Id == id,
      cancellationToken: cancellationToken
    );

    return result.DeletedCount > 0;
  }
}
