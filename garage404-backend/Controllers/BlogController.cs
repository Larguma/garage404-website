using garage404_backend.Models;
using garage404_backend.Models.Utils;
using garage404_backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson;

namespace garage404_backend.Controllers;

[Route("v1/[controller]")]
[ApiController]
public class BlogController(BlogService blogService, UsersService usersService) : ControllerBase
{
  [HttpGet]
  public async Task<IActionResult> GetArticles(CancellationToken cancellationToken)
  {
    return Ok(await blogService.GetAsync(cancellationToken));
  }

  [HttpGet("{id}")]
  public async Task<IActionResult> GetArticle(string id, CancellationToken cancellationToken)
  {
    if (!ObjectId.TryParse(id, out var articleId))
    {
      return BadRequest("Article id is invalid.");
    }

    var article = await blogService.GetAsync(articleId, cancellationToken);
    return article is null ? NotFound() : Ok(article);
  }

  [Authorize]
  [HttpPost]
  public async Task<IActionResult> CreateArticle([FromBody] Blog article, CancellationToken cancellationToken)
  {
    if (!await Authentication.IsAdminAsync(User, usersService))
    {
      return Forbid();
    }

    article.UpdatedAt = DateTimeOffset.UtcNow;
    await blogService.CreateAsync(article, cancellationToken);
    return CreatedAtAction(nameof(GetArticle), new { id = article.IdAsString }, article);
  }

  [Authorize]
  [HttpPut("{id}")]
  public async Task<IActionResult> UpdateArticle(string id, [FromBody] Blog article, CancellationToken cancellationToken)
  {
    if (!await Authentication.IsAdminAsync(User, usersService))
    {
      return Forbid();
    }

    if (!ObjectId.TryParse(id, out var articleId))
    {
      return BadRequest("Article id is invalid.");
    }

    if (!await blogService.UpdateAsync(articleId, article, cancellationToken))
    {
      return NotFound();
    }

    return Ok(article);
  }

  [Authorize]
  [HttpDelete("{id}")]
  public async Task<IActionResult> DeleteArticle(string id, CancellationToken cancellationToken)
  {
    if (!await Authentication.IsAdminAsync(User, usersService))
    {
      return Forbid();
    }

    if (!ObjectId.TryParse(id, out var articleId))
    {
      return BadRequest("Article id is invalid.");
    }

    if (!await blogService.DeleteAsync(articleId, cancellationToken))
    {
      return NotFound();
    }

    return Ok();
  }
}
