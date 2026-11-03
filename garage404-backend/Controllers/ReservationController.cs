using garage404_backend.Interfaces;
using garage404_backend.Models;
using garage404_backend.Models.Utils;
using garage404_backend.Services;
using garage404_backend.Utils;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using MongoDB.Bson;

namespace garage404_backend.Controllers
{
  [Route("v1/[controller]")]
  [ApiController]
  [Authorize(Policy = AuthorizationPolicies.Admin)]
  public class ReservationController(
    ReservationsService reservationsService,
    ServiceTypesService serviceTypesService,
    ILogger<ReservationController> logger,
    IDiscordWebhookService discordWebhookService,
    IGoogleCalendarService googleCalendarService,
    IEmailService emailService
  ) : ControllerBase
  {
    private readonly ReservationsService _reservationsService = reservationsService;
    private readonly ServiceTypesService _serviceTypesService = serviceTypesService;
    private readonly ILogger<ReservationController> _logger = logger;
    private readonly IDiscordWebhookService _discord = discordWebhookService;
    private readonly IGoogleCalendarService _googleCalendar = googleCalendarService;
    private readonly IEmailService _email = emailService;

    /// <summary>Lists all reservations, sorted by preferred date.</summary>
    [HttpGet]
    public async Task<IActionResult> GetReservations(CancellationToken cancellationToken)
    {
      return Ok(await _reservationsService.GetAsync(cancellationToken));
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetReservation(string id, CancellationToken cancellationToken)
    {
      if (!ObjectId.TryParse(id, out var reservationId))
      {
        return this.InvalidId("Reservation");
      }

      var reservation = await _reservationsService.GetAsync(reservationId, cancellationToken);
      return reservation is null ? NotFound() : Ok(reservation);
    }

    /// <summary>Deletes a reservation and its calendar event, if it has one.</summary>
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteReservation(string id, CancellationToken cancellationToken)
    {
      if (!ObjectId.TryParse(id, out var reservationId))
      {
        return this.InvalidId("Reservation");
      }

      var reservation = await _reservationsService.GetAsync(reservationId, cancellationToken);
      if (reservation is null)
      {
        return NotFound();
      }

      if (reservation.CalEventId is not null)
      {
        try
        {
          await _googleCalendar.DeleteReservationEventAsync(reservation.CalEventId, cancellationToken);
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
          _logger.LogWarning(e, "Could not delete the calendar event of reservation {Id}", id);
        }
      }

      await _reservationsService.DeleteAsync(reservationId, cancellationToken);
      return NoContent();
    }

    /// <summary>Public endpoint used by the website to request an appointment.</summary>
    [AllowAnonymous]
    [HttpPost]
    [EnableRateLimiting(RateLimitPolicies.PublicWrite)]
    public async Task<IActionResult> CreateReservation(
      [FromBody] Reservation reservation,
      CancellationToken cancellationToken
    )
    {
      var serviceTypeName = await _serviceTypesService.GetNameAsync(reservation.ServiceType, cancellationToken);
      if (serviceTypeName is null)
      {
        return this.FieldError(nameof(Reservation.ServiceType), $"Unknown service type {reservation.ServiceType}.");
      }

      if (reservation.PreferredDate < DateTime.UtcNow.Date)
      {
        return this.FieldError(nameof(Reservation.PreferredDate), "The preferred date must not be in the past.");
      }

      // These fields are managed by the backend.
      reservation.ActualDate = null;
      reservation.CalEventId = null;
      await _reservationsService.CreateAsync(reservation, cancellationToken);

      // The reservation is saved at this point: a notification failure must not make the client retry.
      try
      {
        await _discord.SendEmbedAsync(null, BuildCreatedEmbed(reservation, serviceTypeName));
      }
      catch (Exception e)
      {
        _logger.LogWarning(e, "Reservation {Id} was saved but the Discord notification failed", reservation.Id);
      }

      return CreatedAtAction(nameof(GetReservation), new { id = reservation.IdAsString }, reservation);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateReservation(
      string id,
      [FromBody] Reservation reservation,
      CancellationToken cancellationToken
    )
    {
      if (!ObjectId.TryParse(id, out var reservationId))
      {
        return this.InvalidId("Reservation");
      }

      if (!await _serviceTypesService.ExistsAsync(reservation.ServiceType, cancellationToken))
      {
        return this.FieldError(nameof(Reservation.ServiceType), $"Unknown service type {reservation.ServiceType}.");
      }

      var existing = await _reservationsService.GetAsync(reservationId, cancellationToken);
      if (existing is null)
      {
        return NotFound();
      }

      // Keep the link to the calendar event, so that accepting again updates it instead of creating a new one.
      reservation.CalEventId ??= existing.CalEventId;
      await _reservationsService.UpdateAsync(reservationId, reservation, cancellationToken);
      return Ok(reservation);
    }

    /// <summary>
    /// Accepts a reservation: the body is the appointment date as Europe/Zurich wall-clock time
    /// (for example <c>"2027-01-15T09:00:00"</c>). Creates or updates the calendar event, then emails the customer.
    /// </summary>
    [HttpPut("{id}/accept")]
    public async Task<IActionResult> AcceptReservation(
      string id,
      [FromBody] DateTimeOffset actualDate,
      CancellationToken cancellationToken
    )
    {
      if (!ObjectId.TryParse(id, out var reservationId))
      {
        return this.InvalidId("Reservation");
      }

      var reservation = await _reservationsService.GetAsync(reservationId, cancellationToken);
      if (reservation == null)
      {
        return NotFound();
      }

      try
      {
        reservation.ActualDate = _googleCalendar.ConvertZurichTimeToUtc(actualDate.DateTime);
      }
      catch (ArgumentException e)
      {
        return this.FieldError("actualDate", e.Message);
      }

      reservation.CalEventId = await _googleCalendar.UpsertReservationEventAsync(reservation, cancellationToken);
      await _reservationsService.UpdateAsync(reservationId, reservation, cancellationToken);

      // The reservation is accepted at this point: report a failed email instead of failing the whole request.
      var emailSent = true;
      try
      {
        await _email.SendReservationAcceptedAsync(reservation, cancellationToken);
      }
      catch (Exception e)
      {
        emailSent = false;
        _logger.LogError(e, "Reservation {Id} was accepted but the confirmation email failed", id);
      }

      return Ok(new AcceptReservationResult(reservation, emailSent));
    }

    private static DiscordEmbed BuildCreatedEmbed(Reservation reservation, string serviceTypeName) =>
      new()
      {
        Title = serviceTypeName + " Reservation Created",
        Description =
          $"A new reservation has been created by {reservation.Name} ({reservation.Email}).\n\n"
          + $"**Phone:** {reservation.Phone}\n"
          + $"**Preferred Date:** {reservation.PreferredDate:yyyy-MM-dd HH:mm}\n"
          + $"**Vehicle:** {reservation.Vehicle}\n"
          + $"**Details:** {reservation.Details ?? "N/A"}",
        Color = 0x00FF00,
        Timestamp = DateTime.UtcNow.ToString("o"),
      };
  }

  public sealed record AcceptReservationResult(Reservation Reservation, bool EmailSent);
}
