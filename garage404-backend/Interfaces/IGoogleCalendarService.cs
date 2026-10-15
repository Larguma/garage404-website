using garage404_backend.Models;

namespace garage404_backend.Services;

public interface IGoogleCalendarService
{
  DateTime ConvertZurichTimeToUtc(DateTime localDateTime);

  Task<string> UpsertReservationEventAsync(Reservation reservation, CancellationToken cancellationToken);
}
