using garage404_backend.Services;
using Microsoft.AspNetCore.Authorization;

namespace garage404_backend.Utils;

public static class AuthorizationPolicies
{
  public const string Admin = "Admin";
}

public static class RateLimitPolicies
{
  public const string Auth = "auth";
  public const string PublicWrite = "public-write";
}

public sealed class AdminRequirement : IAuthorizationRequirement;

/// <summary>
/// Grants the <see cref="AuthorizationPolicies.Admin"/> policy when the Firebase user is flagged as admin in MongoDB.
/// </summary>
public sealed class AdminAuthorizationHandler(UsersService usersService) : AuthorizationHandler<AdminRequirement>
{
  protected override async Task HandleRequirementAsync(
    AuthorizationHandlerContext context,
    AdminRequirement requirement
  )
  {
    var firebaseUid = context.User.FindFirst("user_id")?.Value;
    if (string.IsNullOrEmpty(firebaseUid))
    {
      return;
    }

    var user = await usersService.GetAsync(firebaseUid);
    if (user?.IsAdmin == true)
    {
      context.Succeed(requirement);
    }
  }
}
