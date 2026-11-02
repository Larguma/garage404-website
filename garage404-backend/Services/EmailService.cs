using System.Net;
using System.Net.Mail;
using System.Text.Encodings.Web;
using garage404_backend.Interfaces;
using garage404_backend.Models;
using Microsoft.Extensions.Options;

namespace garage404_backend.Services;

public sealed class EmailService(
  IOptions<EmailSettings> emailSettings,
  IOptions<GoogleCalendarSettings> calendarSettings,
  IWebHostEnvironment environment,
  ServiceTypesService serviceTypesService
) : IEmailService
{
  private readonly EmailSettings _settings = emailSettings.Value;
  private readonly GoogleCalendarSettings _calendarSettings = calendarSettings.Value;
  private readonly ServiceTypesService _serviceTypesService = serviceTypesService;
  private readonly string _templatePath = Path.Combine(
    environment.ContentRootPath,
    "EmailTemplates",
    "ReservationAccepted.html"
  );

  public async Task SendReservationAcceptedAsync(Reservation reservation, CancellationToken cancellationToken)
  {
    if (reservation.ActualDate is not DateTime actualDate)
    {
      throw new InvalidOperationException(
        "The reservation must have an actual date before sending a confirmation email."
      );
    }

    if (
      string.IsNullOrWhiteSpace(_settings.Username)
      || string.IsNullOrWhiteSpace(_settings.Password)
      || string.IsNullOrWhiteSpace(_settings.FromAddress)
    )
    {
      throw new InvalidOperationException(
        "Email settings are incomplete. Configure Email__Username, Email__Password, and Email__FromAddress."
      );
    }

    var timeZone = TimeZoneInfo.FindSystemTimeZoneById(_calendarSettings.TimeZone);
    var appointment = TimeZoneInfo.ConvertTime(
      new DateTimeOffset(DateTime.SpecifyKind(actualDate, DateTimeKind.Utc)),
      timeZone
    );
    var serviceTypes = await _serviceTypesService.GetAsync(reservation.ServiceType);
    var serviceType =
      serviceTypes.FirstOrDefault()?.Name
      ?? throw new InvalidOperationException($"No service type found for {reservation.ServiceType}.");
    var template = await File.ReadAllTextAsync(_templatePath, cancellationToken);
    var body = template
      .Replace("{{NAME}}", HtmlEncoder.Default.Encode(reservation.Name))
      .Replace("{{DATE}}", HtmlEncoder.Default.Encode(appointment.ToString("dd.MM.yyyy HH:mm")))
      .Replace("{{MOTO}}", HtmlEncoder.Default.Encode(reservation.Vehicle))
      .Replace("{{SERVICE}}", HtmlEncoder.Default.Encode(serviceType))
      .Replace("{{DURATION}}", _calendarSettings.DurationMinutes.ToString());

    using var message = new MailMessage
    {
      From = new MailAddress(_settings.FromAddress, _settings.FromName),
      Subject = "Confirmation de votre rendez-vous - Garage404",
      Body = body,
      IsBodyHtml = true,
    };
    message.To.Add(new MailAddress(reservation.Email));

    using var client = new SmtpClient(_settings.Host, _settings.Port)
    {
      EnableSsl = true,
      DeliveryMethod = SmtpDeliveryMethod.Network,
      UseDefaultCredentials = false,
      Credentials = new NetworkCredential(_settings.Username, _settings.Password),
    };

    await client.SendMailAsync(message, cancellationToken);
  }
}
