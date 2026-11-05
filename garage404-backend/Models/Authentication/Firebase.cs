using System.Text.Json.Serialization;

namespace garage404_backend.Models.Authentication;

public class LoginInfo
{
  public string? Username { get; set; }
  public string? Password { get; set; }
}

public class FireBaseLoginInfo
{
  [JsonPropertyName("email")]
  public string? Email { get; set; }

  [JsonPropertyName("password")]
  public string? Password { get; set; }

  [JsonPropertyName("returnSecureToken")]
  public bool ReturnSecureToken { get; set; } = true;
}

public class GoogleToken
{
  public string? Kind { get; set; }
  public string? LocalId { get; set; }
  public string? Email { get; set; }
  public string? DisplayName { get; set; }
  public string? IdToken { get; set; }
  public bool Registered { get; set; }
  public string? RefreshToken { get; set; }
  public string? ExpiresIn { get; set; }
}

public class Token
{
  public string? Token_type { get; set; }
  public int Expires_in { get; set; }
  public int Ext_expires_in { get; set; }
  public string? Access_token { get; set; }
  public string? Id_token { get; set; }
  public string? Refresh_token { get; set; }
}

public class LoginDetail
{
  public string? FirebaseId { get; set; }
  public string? MongoId { get; set; }
  public string? Email { get; set; }
  public bool IsAdmin { get; set; }
  public DateTime RespondedAt { get; set; }
}
