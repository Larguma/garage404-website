using garage404_backend.Models;
using garage404_backend.Models.Utils;
using garage404_backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson;

namespace garage404_backend.Controllers
{
  [Route("v1/[controller]")]
  [ApiController]
  public class BikeController(
      BikesService bikesService,
      BikeImageStorageService imageStorageService,
      UsersService usersService,
      ILogger<BikeController> logger) : ControllerBase
  {
    private const long MaxImageSizeBytes = 10 * 1024 * 1024;
    private const long MaxRequestSizeBytes = 50 * 1024 * 1024;
    private const int MaxImageCount = 10;

    [HttpGet]
    public async Task<IActionResult> GetBikes(CancellationToken cancellationToken)
    {
      var bikes = await bikesService.GetAsync(cancellationToken);
      return Ok(bikes);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetBike(string id, CancellationToken cancellationToken)
    {
      if (!ObjectId.TryParse(id, out var bikeId))
      {
        return BadRequest("Bike id is invalid.");
      }

      var bike = await bikesService.GetAsync(bikeId, cancellationToken);
      return bike is null ? NotFound() : Ok(bike);
    }

    [Authorize]
    [HttpPost]
    public async Task<IActionResult> CreateBike(Bike bike)
    {
      if (!await Authentication.IsAdminAsync(User, usersService))
      {
        return Forbid();
      }

      await bikesService.CreateAsync(bike);
      return CreatedAtAction(nameof(GetBike), new { id = bike.Id.ToString() }, bike);
    }

    [Authorize]
    [HttpPost("{id}/images")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(MaxRequestSizeBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = MaxRequestSizeBytes)]
    public async Task<IActionResult> UploadImages(
        string id,
        IFormFileCollection images,
        CancellationToken cancellationToken)
    {
      if (!await Authentication.IsAdminAsync(User, usersService))
      {
        return Forbid();
      }

      if (!ObjectId.TryParse(id, out var bikeId))
      {
        return BadRequest("Bike id is invalid.");
      }

      if (images is null ||
          images.Count is 0 or > MaxImageCount ||
          images.Any(image => image.Length is 0 or > MaxImageSizeBytes) ||
          images.Sum(image => image.Length) > MaxRequestSizeBytes ||
          images.Any(image => !imageStorageService.IsSupportedImage(image)))
      {
        return BadRequest("Provide up to 10 JPEG, PNG, GIF, or WebP images, each no larger than 10 MB.");
      }

      var bike = await bikesService.GetAsync(bikeId, cancellationToken);
      if (bike is null)
      {
        return NotFound();
      }

      var imageReferences = new List<string>(images.Count);
      try
      {
        foreach (var image in images)
        {
          imageReferences.Add(await imageStorageService.SaveAsync(image, cancellationToken));
        }

        if (!await bikesService.AddImagesAsync(bikeId, imageReferences, cancellationToken))
        {
          DeleteUploadedImages(imageReferences);
          return NotFound();
        }
      }
      catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
      {
        DeleteUploadedImages(imageReferences);
        throw;
      }
      catch (Exception exception)
      {
        logger.LogError(exception, "Failed to save images for bike {BikeId}", id);
        DeleteUploadedImages(imageReferences);
        return Problem("The bike images could not be saved.");
      }

      return Ok(imageReferences);
    }

    private void DeleteUploadedImages(IEnumerable<string> imageReferences)
    {
      foreach (var imageReference in imageReferences)
      {
        try
        {
          imageStorageService.Delete(imageReference);
        }
        catch (Exception exception)
        {
          logger.LogError(exception, "Failed to remove uploaded bike image {ImageReference}", imageReference);
        }
      }
    }
  }
}
