using garage404_backend.Models;
using garage404_backend.Models.Utils;
using garage404_backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson;

namespace garage404_backend.Controllers
{
  [Route("api/[controller]")]
  [ApiController]
  public class ReservationController(
    ReservationsService reservationsService,
    ServiceTypesService serviceTypesService,
    UsersService usersService,
    ILogger<ReservationController> logger,
    IDiscordWebhookService discordWebhookService) : ControllerBase
  {
    private readonly ReservationsService _reservationsService = reservationsService;
    private readonly ServiceTypesService _serviceTypesService = serviceTypesService;
    private readonly UsersService _usersService = usersService;
    private readonly ILogger<ReservationController> _logger = logger;
    private readonly IDiscordWebhookService _discord = discordWebhookService;

    [Authorize]
    [HttpGet]
    public async Task<IActionResult> GetReservations()
    {
      if (!await Authentication.IsAdminAsync(User, _usersService))
      {
        return Forbid();
      }

      try
      {
        var reservations = await _reservationsService.GetAsync();
        return Ok(reservations);
      }
      catch (Exception e)
      {
        _logger.LogError(e, "Error fetching reservations");
        return BadRequest();
      }
    }

    [Authorize]
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteReservation(string id)
    {
      if (!await Authentication.IsAdminAsync(User, _usersService))
      {
        return Forbid();
      }

      try
      {
        await _reservationsService.DeleteAsync(ObjectId.Parse(id));
        return Ok();
      }
      catch (Exception e)
      {
        _logger.LogError(e, "Error deleting reservation with id {id}", id);
        return BadRequest();
      }
    }

    [HttpPost]
    public async Task<IActionResult> CreateReservation([FromBody] Reservation reservation)
    {
      if (reservation == null)
      {
        return BadRequest("Reservation data is required.");
      }

      try
      {
        var serviceTypes = await _serviceTypesService.GetAsync(reservation.ServiceType);
        var serviceTypeName = serviceTypes[0].Name;
        await _reservationsService.CreateAsync(reservation);

        var embed = new DiscordEmbed
        {
          Title = serviceTypeName + " Reservation Created",
          Description = $"A new reservation has been created by {reservation.Name} ({reservation.Email}).\n\n" +
                        $"**Phone:** {reservation.Phone}\n" +
                        $"**Preferred Date:** {reservation.PrefferedDate:yyyy-MM-dd HH:mm}\n" +
                        $"**Vehicle:** {reservation.Vehicle}\n" +
                        $"**Details:** {reservation.Details ?? "N/A"}",
          Color = 0x00FF00,
          Timestamp = DateTime.UtcNow.ToString("o")
        };

        await _discord.SendEmbedAsync(null, embed);
        return Ok();
      }
      catch (Exception e)
      {
        _logger.LogError(e, "Error creating reservation");
        return BadRequest();
      }
    }

    [Authorize]
    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateReservation(string id, [FromBody] Reservation reservation)
    {
      if (!await Authentication.IsAdminAsync(User, _usersService))
      {
        return Forbid();
      }

      if (reservation == null)
      {
        return BadRequest("Reservation data is required.");
      }

      try
      {
        await _reservationsService.UpdateAsync(ObjectId.Parse(id), reservation);
        return Ok();
      }
      catch (Exception e)
      {
        _logger.LogError(e, "Error updating reservation with id {id}", id);
        return BadRequest();
      }
    }

    [Authorize]
    [HttpPut("{id}/accept")]
    public async Task<IActionResult> AcceptReservation(string id, [FromBody] DateTime actualDate)
    {
      if (!await Authentication.IsAdminAsync(User, _usersService))
      {
        return Forbid();
      }

      try
      {
        var reservation = await _reservationsService.GetAsync(ObjectId.Parse(id));
        if (reservation == null)
        {
          return NotFound($"No reservation found with id {id}");
        }

        reservation.ActualDate = actualDate;
        await _reservationsService.UpdateAsync(ObjectId.Parse(id), reservation);
        return Ok();
      }
      catch (Exception e)
      {
        _logger.LogError(e, "Error accepting reservation with id {id}", id);
        return BadRequest();
      }
    }
  }
}
