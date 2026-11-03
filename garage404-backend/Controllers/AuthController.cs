using garage404_backend.Models.Authentication;
using garage404_backend.Utils;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace garage404_backend.Controllers
{
  [Route("v1/[controller]")]
  [ApiController]
  public class AuthController(
    IConfiguration configuration,
    IHttpClientFactory httpClientFactory,
    ILogger<AuthController> logger
  ) : ControllerBase
  {
    private const string SignInUri = "https://identitytoolkit.googleapis.com/v1/accounts:signInWithPassword";

    /// <summary>Exchanges an email and a password for a Firebase ID token (OAuth2 password flow).</summary>
    [HttpPost]
    [Consumes("application/x-www-form-urlencoded", "multipart/form-data")]
    [EnableRateLimiting(RateLimitPolicies.Auth)]
    [ProducesResponseType<Token>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<Token>> GetToken([FromForm] LoginInfo loginInfo, CancellationToken cancellationToken)
    {
      if (string.IsNullOrWhiteSpace(loginInfo.Username) || string.IsNullOrWhiteSpace(loginInfo.Password))
      {
        return Problem(title: "Username and password are required.", statusCode: StatusCodes.Status400BadRequest);
      }

      var apiKey =
        configuration["Firebase:ApiKey"] ?? throw new InvalidOperationException("Firebase:ApiKey is not configured.");

      var client = httpClientFactory.CreateClient();
      FireBaseLoginInfo fireBaseLoginInfo = new() { Email = loginInfo.Username, Password = loginInfo.Password };
      using var response = await client.PostAsJsonAsync(
        $"{SignInUri}?key={Uri.EscapeDataString(apiKey)}",
        fireBaseLoginInfo,
        cancellationToken
      );

      if (!response.IsSuccessStatusCode)
      {
        if ((int)response.StatusCode >= 500)
        {
          logger.LogError("Firebase sign-in failed with status {StatusCode}", response.StatusCode);
          return Problem(
            title: "The authentication provider is unavailable.",
            statusCode: StatusCodes.Status503ServiceUnavailable
          );
        }

        // Same answer for an unknown account and a wrong password, to avoid account enumeration.
        return Problem(title: "Invalid username or password.", statusCode: StatusCodes.Status401Unauthorized);
      }

      var encoded = await response.Content.ReadFromJsonAsync<GoogleToken>(cancellationToken);
      if (string.IsNullOrEmpty(encoded?.IdToken))
      {
        logger.LogError("Firebase sign-in succeeded but returned no ID token");
        return Problem(
          title: "Unable to parse the authentication response.",
          statusCode: StatusCodes.Status502BadGateway
        );
      }

      return Ok(
        new Token
        {
          Token_type = "Bearer",
          Access_token = encoded.IdToken,
          Id_token = encoded.IdToken,
          Refresh_token = encoded.RefreshToken,
          Expires_in = int.TryParse(encoded.ExpiresIn, out var expiresIn) ? expiresIn : 3600,
        }
      );
    }
  }
}
