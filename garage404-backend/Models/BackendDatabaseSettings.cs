namespace garage404_backend.Models;

public class BackendDatabaseSettings
{
  public string ConnectionString { get; set; } = null!;

  public string DatabaseName { get; set; } = null!;

  public string UsersCollectionName { get; set; } = null!;
  public string ServiceTypesCollectionName { get; set; } = null!;
  public string ReservationsCollectionName { get; set; } = null!;
  public string BikesCollectionName { get; set; } = null!;
}
