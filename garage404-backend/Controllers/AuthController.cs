using System.Text.Json;
using garage404_backend.Models.Authentication;
using Microsoft.AspNetCore.Mvc;

namespace garage404_backend.Controllers
{
  [Route("v1/[controller]")]
  [ApiController]
  public class AuthController(IConfiguration configuration) : ControllerBase
  {
    private readonly IConfiguration _configuration = configuration;

    [HttpPost]
    public async Task<ActionResult> GetToken([FromForm] LoginInfo loginInfo)
    {
      var apiKey = _configuration["Firebase:ApiKey"] ?? throw new InvalidOperationException("Firebase:ApiKey is not configured.");
      var uri = $"https://www.googleapis.com/identitytoolkit/v3/relyingparty/verifyPassword?key={apiKey}";

      using HttpClient client = new();
      FireBaseLoginInfo fireBaseLoginInfo = new()
      {
        Email = loginInfo.Username,
        Password = loginInfo.Password
      };
      var result = await client.PostAsJsonAsync(uri, fireBaseLoginInfo,
          new JsonSerializerOptions()
          { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

      var encoded = await result.Content.ReadFromJsonAsync<GoogleToken>();
      if (encoded is null)
      {
        return BadRequest("Unable to parse authentication response.");
      }

      Token token = new()
      {
        Token_type = "Bearer",
        Access_token = encoded.IdToken,
        Id_token = encoded.IdToken,
        Expires_in = int.Parse(encoded.ExpiresIn ?? "3600"),
        refresh_token = encoded.RefreshToken
      };
      return Ok(token);
    }
  }
}
