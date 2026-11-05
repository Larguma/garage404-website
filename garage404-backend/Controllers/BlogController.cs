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
  [HttpGet]
  [EndpointDescription("Lists the published articles. Admins also get drafts (no PublishedAt) and scheduled articles.")]
  [ProducesResponseType(StatusCodes.Status200OK)]
  public async Task<IActionResult> GetArticles(CancellationToken cancellationToken)
  {
    return Ok(await blogService.GetAsync(await IsAdminAsync(), cancellationToken));
  }

  [HttpGet("{id}")]
  [EndpointDescription("Gets an article by its ID")]
  [ProducesResponseType(StatusCodes.Status200OK)]
  [ProducesResponseType(StatusCodes.Status400BadRequest)]
  [ProducesResponseType(StatusCodes.Status404NotFound)]
  public async Task<IActionResult> GetArticle(string id, CancellationToken cancellationToken)
  {
    if (!ObjectId.TryParse(id, out var articleId))
    {
      return this.InvalidId("Article");
    }

    return await VisibleOrNotFound(await blogService.GetAsync(articleId, cancellationToken));
  }

  [HttpGet("slug/{slug}")]
  [EndpointDescription("Gets an article by its slug.")]
  [ProducesResponseType(StatusCodes.Status200OK)]
  [ProducesResponseType(StatusCodes.Status404NotFound)]
  public async Task<IActionResult> GetArticleBySlug(string slug, CancellationToken cancellationToken)
  {
    return await VisibleOrNotFound(await blogService.GetBySlugAsync(slug, cancellationToken));
  }

  [Authorize(Policy = AuthorizationPolicies.Admin)]
  [HttpPost]
  [EndpointDescription("Creates a new article.")]
  [ProducesResponseType(StatusCodes.Status201Created)]
  [ProducesResponseType(StatusCodes.Status409Conflict)]
  public async Task<IActionResult> CreateArticle([FromBody] Blog article, CancellationToken cancellationToken)
  {
    article.Slug = article.Title?.Trim().ToLowerInvariant() + "-" + Guid.NewGuid().ToString("N")[..8];

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
  [EndpointDescription("Updates an existing article.")]
  [ProducesResponseType(StatusCodes.Status200OK)]
  [ProducesResponseType(StatusCodes.Status400BadRequest)]
  [ProducesResponseType(StatusCodes.Status404NotFound)]
  [ProducesResponseType(StatusCodes.Status409Conflict)]
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
  [EndpointDescription("Deletes an article by its ID.")]
  [ProducesResponseType(StatusCodes.Status204NoContent)]
  [ProducesResponseType(StatusCodes.Status400BadRequest)]
  [ProducesResponseType(StatusCodes.Status404NotFound)]
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
