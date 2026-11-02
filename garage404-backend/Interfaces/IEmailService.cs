using garage404_backend.Models;

namespace garage404_backend.Interfaces;

public interface IEmailService
{
  Task SendReservationAcceptedAsync(Reservation reservation, CancellationToken cancellationToken);
}
