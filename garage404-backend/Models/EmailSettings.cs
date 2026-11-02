namespace garage404_backend.Models;

public sealed class EmailSettings
{
  public string Host { get; set; } = "mail.infomaniak.com";
  public int Port { get; set; } = 587;
  public string Username { get; set; } = "";
  public string Password { get; set; } = "";
  public string FromAddress { get; set; } = "";
  public string FromName { get; set; } = "Garage404";
}
