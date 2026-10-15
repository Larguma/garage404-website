namespace garage404_backend.Services;

public sealed class BikeImageStorageService(IWebHostEnvironment environment)
{
  private static readonly HashSet<string> SupportedContentTypes =
  [
    "image/jpeg",
    "image/png",
    "image/gif",
    "image/webp"
  ];

  private readonly string _imageDirectory =
      Path.Combine(environment.WebRootPath, "images");

  public bool IsSupportedImage(IFormFile file) =>
      SupportedContentTypes.Contains(file.ContentType) &&
      Path.GetExtension(file.FileName).ToLowerInvariant() switch
      {
        ".jpg" or ".jpeg" => file.ContentType == "image/jpeg",
        ".png" => file.ContentType == "image/png",
        ".gif" => file.ContentType == "image/gif",
        ".webp" => file.ContentType == "image/webp",
        _ => false
      };

  public async Task<string> SaveAsync(IFormFile file, CancellationToken cancellationToken)
  {
    Directory.CreateDirectory(_imageDirectory);

    var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
    var fileName = $"{Guid.NewGuid():N}{extension}";
    var filePath = Path.Combine(_imageDirectory, fileName);
    var fileCreated = false;

    try
    {
      await using var destination = new FileStream(
          filePath,
          FileMode.CreateNew,
          FileAccess.Write,
          FileShare.None,
          bufferSize: 81920,
          useAsync: true);
      fileCreated = true;
      await file.CopyToAsync(destination, cancellationToken);
    }
    catch
    {
      if (fileCreated)
      {
        File.Delete(filePath);
      }

      throw;
    }

    return $"/images/{fileName}";
  }

  public void Delete(string imageReference)
  {
    const string imagePrefix = "/images/";
    if (!imageReference.StartsWith(imagePrefix, StringComparison.Ordinal))
    {
      throw new ArgumentException("Image reference is not a bike image path.", nameof(imageReference));
    }

    var fileName = imageReference[imagePrefix.Length..];
    if (Path.GetFileName(fileName) != fileName)
    {
      throw new ArgumentException("Image reference contains an invalid file name.", nameof(imageReference));
    }

    var filePath = Path.Combine(_imageDirectory, fileName);
    if (File.Exists(filePath))
    {
      File.Delete(filePath);
    }
  }
}
