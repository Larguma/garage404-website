using System.Net;
using garage404_backend.Models;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Calendar.v3;
using Google.Apis.Calendar.v3.Data;
using Google.Apis.Services;
using Microsoft.Extensions.Options;

namespace garage404_backend.Services;

public sealed class GoogleCalendarService : IGoogleCalendarService, IDisposable
{

  private readonly ServiceTypesService _serviceTypesService;
  private readonly CalendarService _calendarService;
  private readonly GoogleCalendarSettings _settings;
  private readonly TimeZoneInfo _timeZone;

  public GoogleCalendarService(
    ServiceTypesService serviceTypesService,
    IOptions<GoogleCalendarSettings> settings,
    IWebHostEnvironment environment)
  {
    _serviceTypesService = serviceTypesService;
    _settings = settings.Value;
    _timeZone = TimeZoneInfo.FindSystemTimeZoneById(_settings.TimeZone);

    var credentialPath = Path.Combine(environment.ContentRootPath, "garage404-68a8f.json");
    var credential = CredentialFactory
      .FromFile(credentialPath, JsonCredentialParameters.ServiceAccountCredentialType)
      .CreateScoped(CalendarService.Scope.CalendarEvents);

    _calendarService = new CalendarService(new BaseClientService.Initializer
    {
      HttpClientInitializer = credential,
      ApplicationName = "Garage404"
    });
  }

  public async Task<string> UpsertReservationEventAsync(
    Reservation reservation,
    CancellationToken cancellationToken)
  {
    if (reservation.ActualDate is not DateTime actualDate)
    {
      throw new InvalidOperationException("The reservation must have an actual date before creating a calendar event.");
    }

    var eventId = reservation.CalEventId ?? reservation.Id.ToString().ToLowerInvariant();
    var start = GetStartDateTime(actualDate);
    var serviceTypes = await _serviceTypesService.GetAsync(reservation.ServiceType);
    var serviceTypeName = serviceTypes[0].Name;
    var calendarEvent = new Event
    {
      Id = eventId,
      Summary = $"Garage404 reservation - {reservation.Name}",
      Description =
        $"Vehicle: {reservation.Vehicle}\n" +
        $"Service type: {serviceTypeName}\n" +
        $"Name: {reservation.Name}\n" +
        $"Email: {reservation.Email}\n" +
        $"Phone: {reservation.Phone}\n" +
        $"Details: {reservation.Details ?? "N/A"}",
      Start = new EventDateTime
      {
        DateTimeDateTimeOffset = start,
        TimeZone = _settings.TimeZone
      },
      End = new EventDateTime
      {
        DateTimeDateTimeOffset = start.AddMinutes(_settings.DurationMinutes),
        TimeZone = _settings.TimeZone
      }
    };

    try
    {
      await _calendarService.Events
        .Update(calendarEvent, _settings.CalendarId, eventId)
        .ExecuteAsync(cancellationToken);
    }
    catch (Google.GoogleApiException exception) when (exception.HttpStatusCode == HttpStatusCode.NotFound)
    {
      try
      {
        await _calendarService.Events
          .Insert(calendarEvent, _settings.CalendarId)
          .ExecuteAsync(cancellationToken);
      }
      catch (Google.GoogleApiException insertException) when (insertException.HttpStatusCode == HttpStatusCode.Conflict)
      {
        await _calendarService.Events
          .Update(calendarEvent, _settings.CalendarId, eventId)
          .ExecuteAsync(cancellationToken);
      }
    }

    return eventId;
  }

  public DateTime ConvertZurichTimeToUtc(DateTime localDateTime)
  {
    var zurichDateTime = DateTime.SpecifyKind(localDateTime, DateTimeKind.Unspecified);
    if (_timeZone.IsInvalidTime(zurichDateTime))
    {
      throw new ArgumentException("The supplied time does not exist in the configured time zone.", nameof(localDateTime));
    }

    if (_timeZone.IsAmbiguousTime(zurichDateTime))
    {
      throw new ArgumentException("The supplied time occurs twice in the configured time zone.", nameof(localDateTime));
    }

    return TimeZoneInfo.ConvertTimeToUtc(zurichDateTime, _timeZone);
  }

  public void Dispose() => _calendarService.Dispose();

  private DateTimeOffset GetStartDateTime(DateTime dateTime)
  {
    if (dateTime.Kind != DateTimeKind.Unspecified)
    {
      return TimeZoneInfo.ConvertTime(new DateTimeOffset(dateTime), _timeZone);
    }

    var localDateTime = DateTime.SpecifyKind(dateTime, DateTimeKind.Unspecified);
    return new DateTimeOffset(localDateTime, _timeZone.GetUtcOffset(localDateTime));
  }
}
