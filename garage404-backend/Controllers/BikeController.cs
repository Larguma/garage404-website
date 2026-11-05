using garage404_backend.Models;
using garage404_backend.Services;
using garage404_backend.Utils;
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
    ILogger<BikeController> logger
  ) : ControllerBase
  {
    private const long MaxImageSizeBytes = 10 * 1024 * 1024;
    private const long MaxRequestSizeBytes = 50 * 1024 * 1024;
    private const int MaxImageCount = 10;

    [HttpGet]
    [EndpointDescription(
      "Gets all bikes."
        + " The bikes are returned in the order they were added to the database,"
        + " with the most recently added bike first."
    )]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetBikes(CancellationToken cancellationToken)
    {
      var bikes = await bikesService.GetAsync(cancellationToken);

      return Ok(bikes);
    }

    [HttpGet("{id}")]
    [EndpointDescription("Gets a bike by its ID.")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetBike(string id, CancellationToken cancellationToken)
    {
      if (!ObjectId.TryParse(id, out var bikeId))
      {
        return this.InvalidId("Bike");
      }

      var bike = await bikesService.GetAsync(bikeId, cancellationToken);
      return bike is null ? NotFound() : Ok(bike);
    }

    [Authorize(Policy = AuthorizationPolicies.Admin)]
    [HttpPost]
    [EndpointDescription("Creates a new bike.")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateBike(Bike bike, CancellationToken cancellationToken)
    {
      // Images are added through the upload endpoint only.
      bike.Images = [];
      await bikesService.CreateAsync(bike, cancellationToken);
      return CreatedAtAction(nameof(GetBike), new { id = bike.IdAsString }, bike);
    }

    [Authorize(Policy = AuthorizationPolicies.Admin)]
    [HttpPut("{id}")]
    [EndpointDescription("Updates an existing bike.")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateBike(string id, Bike bike, CancellationToken cancellationToken)
    {
      if (!ObjectId.TryParse(id, out var bikeId))
      {
        return this.InvalidId("Bike");
      }

      var existingBike = await bikesService.GetAsync(bikeId, cancellationToken);
      if (existingBike is null)
      {
        return NotFound();
      }

      // Images are managed through the dedicated endpoints and are kept as they are.
      bike.Id = bikeId;
      bike.Images = existingBike.Images;
      return await bikesService.UpdateAsync(bike, cancellationToken) ? Ok(bike) : NotFound();
    }

    [Authorize(Policy = AuthorizationPolicies.Admin)]
    [HttpDelete("{id}")]
    [EndpointDescription("Deletes a bike by its ID.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteBike(string id, CancellationToken cancellationToken)
    {
      if (!ObjectId.TryParse(id, out var bikeId))
      {
        return this.InvalidId("Bike");
      }

      var bike = await bikesService.GetAsync(bikeId, cancellationToken);
      if (bike is null || !await bikesService.DeleteAsync(bikeId, cancellationToken))
      {
        return NotFound();
      }

      DeleteUploadedImages(bike.Images);
      return NoContent();
    }

    [Authorize(Policy = AuthorizationPolicies.Admin)]
    [HttpPost("{id}/images")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(MaxRequestSizeBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = MaxRequestSizeBytes)]
    [EndpointDescription(
      "Uploads one or more images for a bike. "
        + "The images are stored in the backend's file system and their URLs are added to the bike's Images list."
    )]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UploadImages(
      string id,
      IFormFileCollection images,
      CancellationToken cancellationToken
    )
    {
      if (!ObjectId.TryParse(id, out var bikeId))
      {
        return this.InvalidId("Bike");
      }

      if (
        images is null
        || images.Count is 0 or > MaxImageCount
        || images.Any(image => image.Length is 0 or > MaxImageSizeBytes)
        || images.Sum(image => image.Length) > MaxRequestSizeBytes
        || images.Any(image => !imageStorageService.IsSupportedImage(image))
      )
      {
        return this.FieldError("images", "Provide up to 10 JPEG, PNG, GIF, or WebP images, each no larger than 10 MB.");
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

    [Authorize(Policy = AuthorizationPolicies.Admin)]
    [HttpDelete("{id}/images/{fileName}")]
    [EndpointDescription(
      "The image is removed from the bike's Images list and deleted from the backend's file system. "
        + "The image file name must be provided without any path components."
    )]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteImage(string id, string fileName, CancellationToken cancellationToken)
    {
      if (!ObjectId.TryParse(id, out var bikeId))
      {
        return this.InvalidId("Bike");
      }

      var imageReference = $"/images/{fileName}";
      if (!await bikesService.RemoveImageAsync(bikeId, imageReference, cancellationToken))
      {
        return NotFound();
      }

      DeleteUploadedImages([imageReference]);
      return NoContent();
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
