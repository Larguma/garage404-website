namespace garage404_backend.Models;

public sealed class GoogleCalendarSettings
{
  public string CalendarId { get; set; } = "";
  public int DurationMinutes { get; set; } = 120;
  public string TimeZone { get; set; } = "Europe/Zurich";
}
