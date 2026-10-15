using System.Security.Claims;
using garage404_backend.Models.Authentication;
using garage404_backend.Models.Utils;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using garage404_backend.Services;
using System.Text;

namespace garage404_backend.Controllers
{
  [Route("v1/[controller]")]
  [ApiController]
  [Authorize]
  public class UserController(UsersService usersService, IDiscordWebhookService discord) : ControllerBase
  {
    private readonly UsersService _usersService = usersService;
    private readonly IDiscordWebhookService _discord = discord;

    [HttpGet]
    public async Task<LoginDetail> GetAuthenticatedUserDetail()
    {
      var claimsPrincipal = User;
      var firebaseId = claimsPrincipal.Claims.First(x => x.Type == "user_id").Value;
      var email = claimsPrincipal.Claims.First(x => x.Type == ClaimTypes.Email).Value;

      return new()
      {
        FirebaseId = firebaseId,
        Email = email,
        MongoId = (await _usersService.GetByEmailAsync(email))?.Id.ToString() ?? string.Empty,
        RespondedAt = DateTime.Now
      };
    }

    [HttpPost("set-admin")]
    [Authorize]
    public async Task<IActionResult> SetAdmin(bool isAdmin, string userId)
    {
      if (!await Authentication.IsAdminAsync(User, _usersService))
      {
        return Forbid();
      }

      var user = await _usersService.GetAsync(userId);

      if (user == null)
      {
        return NotFound();
      }

      user.IsAdmin = isAdmin;
      await _usersService.UpdateAsync(user);
      StringBuilder sb = new();
      sb.AppendLine("```ansi");
      sb.AppendLine($"User \x1b[31m{user.Email}\x1b[0m (MongoId: {user.Id}) admin status changed to \x1b[31m{isAdmin}\x1b[0m by \x1b[31m{User.FindFirstValue(ClaimTypes.Email)}\x1b[0m (MongoId: {User.FindFirstValue(ClaimTypes.NameIdentifier)})");
      sb.AppendLine("```");

      await _discord.SendMessageWithRetryAsync(sb.ToString());
      return Ok();

    }
  }
}
