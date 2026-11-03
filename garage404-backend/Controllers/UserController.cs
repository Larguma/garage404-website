using System.Security.Claims;
using System.Text;
using garage404_backend.Interfaces;
using garage404_backend.Models.Authentication;
using garage404_backend.Services;
using garage404_backend.Utils;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson;

namespace garage404_backend.Controllers
{
  [Route("v1/[controller]")]
  [ApiController]
  [Authorize]
  public class UserController(UsersService usersService, IDiscordWebhookService discord) : ControllerBase
  {
    private readonly UsersService _usersService = usersService;
    private readonly IDiscordWebhookService _discord = discord;

    /// <summary>Returns the profile of the authenticated user.</summary>
    [HttpGet]
    public async Task<LoginDetail> GetAuthenticatedUserDetail()
    {
      var firebaseId = User.FindFirstValue("user_id");
      var user = string.IsNullOrEmpty(firebaseId) ? null : await _usersService.GetAsync(firebaseId);

      return new()
      {
        FirebaseId = firebaseId,
        Email = User.FindFirstValue(ClaimTypes.Email),
        MongoId = user?.Id.ToString() ?? string.Empty,
        IsAdmin = user?.IsAdmin == true,
        RespondedAt = DateTime.UtcNow,
      };
    }

    /// <summary>Lists all users, so that an admin can find the id to pass to set-admin.</summary>
    [HttpGet("all")]
    [Authorize(Policy = AuthorizationPolicies.Admin)]
    public async Task<IEnumerable<LoginDetail>> GetUsers()
    {
      var respondedAt = DateTime.UtcNow;
      return (await _usersService.GetAsync()).Select(user => new LoginDetail
      {
        FirebaseId = user.FirebaseUserId,
        MongoId = user.Id.ToString(),
        Email = user.Email,
        IsAdmin = user.IsAdmin,
        RespondedAt = respondedAt,
      });
    }

    /// <summary>Grants or revokes the admin role. <paramref name="userId"/> is the Firebase id or the Mongo id.</summary>
    [HttpPost("set-admin")]
    [Authorize(Policy = AuthorizationPolicies.Admin)]
    public async Task<IActionResult> SetAdmin([FromQuery] bool isAdmin, [FromQuery] string userId)
    {
      var user = ObjectId.TryParse(userId, out var mongoId)
        ? await _usersService.GetAsync(mongoId)
        : await _usersService.GetAsync(userId);

      if (user == null)
      {
        return NotFound();
      }

      var callerFirebaseId = User.FindFirstValue("user_id");
      if (!isAdmin && user.FirebaseUserId == callerFirebaseId)
      {
        return this.FieldError(nameof(isAdmin), "You cannot remove your own admin role.");
      }

      user.IsAdmin = isAdmin;
      await _usersService.UpdateAsync(user);
      StringBuilder sb = new();
      sb.AppendLine("```ansi");
      sb.AppendLine(
        $"User \x1b[31m{user.Email}\x1b[0m (MongoId: {user.Id}) admin status changed to \x1b[31m{isAdmin}\x1b[0m by \x1b[31m{User.FindFirstValue(ClaimTypes.Email)}\x1b[0m (FirebaseId: {callerFirebaseId})"
      );
      sb.AppendLine("```");

      await _discord.SendMessageWithRetryAsync(sb.ToString());
      return Ok();
    }
  }
}
