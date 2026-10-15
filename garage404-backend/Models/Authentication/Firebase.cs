using System;

namespace garage404_backend.Models.Authentication;

public class LoginInfo
{
  public string? Username { get; set; }
  public string? Password { get; set; }
}

public class FireBaseLoginInfo
{
  public string? Email { get; set; }
  public string? Password { get; set; }
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
  internal string? refresh_token;

  public string? Token_type { get; set; }
  public int Expires_in { get; set; }
  public int Ext_expires_in { get; set; }
  public string? Access_token { get; set; }
  public string? Id_token { get; set; }
}

public class LoginDetail
{
  public string? FirebaseId { get; set; }
  public string? MongoId { get; set; }
  public string? Email { get; set; }
  public DateTime RespondedAt { get; set; }
}