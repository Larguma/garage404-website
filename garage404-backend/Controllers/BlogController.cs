using garage404_backend.Models;
using garage404_backend.Services;
using garage404_backend.Utils;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson;

namespace garage404_backend.Controllers;

[Route("v1/[controller]")]
[ApiController]
public class BlogController(BlogService blogService, IAuthorizationService authorizationService) : ControllerBase
{
  /// <summary>
  /// Lists the published articles. Admins also get drafts (no <c>PublishedAt</c>) and scheduled articles.
  /// </summary>
  [HttpGet]
  public async Task<IActionResult> GetArticles(CancellationToken cancellationToken)
  {
    return Ok(await blogService.GetAsync(await IsAdminAsync(), cancellationToken));
  }

  [HttpGet("{id}")]
  public async Task<IActionResult> GetArticle(string id, CancellationToken cancellationToken)
  {
    if (!ObjectId.TryParse(id, out var articleId))
    {
      return this.InvalidId("Article");
    }

    return await VisibleOrNotFound(await blogService.GetAsync(articleId, cancellationToken));
  }

  [HttpGet("slug/{slug}")]
  public async Task<IActionResult> GetArticleBySlug(string slug, CancellationToken cancellationToken)
  {
    return await VisibleOrNotFound(await blogService.GetBySlugAsync(slug, cancellationToken));
  }

  [Authorize(Policy = AuthorizationPolicies.Admin)]
  [HttpPost]
  public async Task<IActionResult> CreateArticle([FromBody] Blog article, CancellationToken cancellationToken)
  {
    if (await blogService.SlugExistsAsync(article.Slug, cancellationToken: cancellationToken))
    {
      return SlugConflict(article.Slug);
    }

    article.UpdatedAt = DateTimeOffset.UtcNow;
    await blogService.CreateAsync(article, cancellationToken);
    return CreatedAtAction(nameof(GetArticle), new { id = article.IdAsString }, article);
  }

  [Authorize(Policy = AuthorizationPolicies.Admin)]
  [HttpPut("{id}")]
  public async Task<IActionResult> UpdateArticle(
    string id,
    [FromBody] Blog article,
    CancellationToken cancellationToken
  )
  {
    if (!ObjectId.TryParse(id, out var articleId))
    {
      return this.InvalidId("Article");
    }

    if (await blogService.SlugExistsAsync(article.Slug, articleId, cancellationToken))
    {
      return SlugConflict(article.Slug);
    }

    if (!await blogService.UpdateAsync(articleId, article, cancellationToken))
    {
      return NotFound();
    }

    return Ok(article);
  }

  [Authorize(Policy = AuthorizationPolicies.Admin)]
  [HttpDelete("{id}")]
  public async Task<IActionResult> DeleteArticle(string id, CancellationToken cancellationToken)
  {
    if (!ObjectId.TryParse(id, out var articleId))
    {
      return this.InvalidId("Article");
    }

    if (!await blogService.DeleteAsync(articleId, cancellationToken))
    {
      return NotFound();
    }

    return NoContent();
  }

  private async Task<bool> IsAdminAsync() =>
    User.Identity?.IsAuthenticated == true
    && (await authorizationService.AuthorizeAsync(User, AuthorizationPolicies.Admin)).Succeeded;

  private async Task<IActionResult> VisibleOrNotFound(Blog? article)
  {
    var isPublished = article?.PublishedAt is DateTimeOffset publishedAt && publishedAt <= DateTimeOffset.UtcNow;
    return article is not null && (isPublished || await IsAdminAsync()) ? Ok(article) : NotFound();
  }

  private ObjectResult SlugConflict(string slug) =>
    Problem(title: $"An article with the slug '{slug}' already exists.", statusCode: StatusCodes.Status409Conflict);
}
