using System.Security.Claims;
using garage404_backend.Services;

namespace garage404_backend.Models.Utils;

public static class Authentication
{
  public static async Task<bool> IsAdminAsync(ClaimsPrincipal principal, UsersService usersService)
  {
    var firebaseUid = principal.FindFirst("user_id")?.Value;
    if (string.IsNullOrEmpty(firebaseUid))
    {
      return false;
    }

    var user = await usersService.GetAsync(firebaseUid);
    return user?.IsAdmin == true;
  }
}
