using garage404_backend.Models;
using garage404_backend.Services;
using garage404_backend.Utils;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace garage404_backend.Controllers
{
  [Route("v1/[controller]")]
  [ApiController]
  [Authorize(Policy = AuthorizationPolicies.Admin)]
  public class ServiceTypeController(ServiceTypesService serviceTypesService) : ControllerBase
  {
    private readonly ServiceTypesService _serviceTypesService = serviceTypesService;

    [HttpPost]
    [Consumes("application/json")]
    [EndpointDescription("Adds several service types at once. Each Type must be unique.")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> AddServiceTypes(
      [FromBody] List<ServiceType> json,
      CancellationToken cancellationToken
    )
    {
      if (json == null || json.Count == 0)
      {
        return this.FieldError("json", "No service types provided.");
      }

      var duplicates = json.GroupBy(x => x.Type).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
      if (duplicates.Count > 0)
      {
        return this.FieldError("json", $"Duplicate service types in the request: {string.Join(", ", duplicates)}.");
      }

      var existingTypes = (await _serviceTypesService.GetAsync(cancellationToken)).Select(x => x.Type).ToHashSet();
      var conflicts = json.Where(x => existingTypes.Contains(x.Type)).Select(x => x.Type).ToList();
      if (conflicts.Count > 0)
      {
        return Problem(
          title: $"These service types already exist: {string.Join(", ", conflicts)}.",
          statusCode: StatusCodes.Status409Conflict
        );
      }

      await _serviceTypesService.CreateAsync(json, cancellationToken);
      return Ok(json);
    }

    [AllowAnonymous]
    [HttpGet]
    [EndpointDescription("Public: the website needs the list to build the reservation form.")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetServiceTypes(CancellationToken cancellationToken)
    {
      return Ok(await _serviceTypesService.GetAsync(cancellationToken));
    }

    [HttpDelete("{type}")]
    [EndpointDescription("Deletes a service type by its Type.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteServiceType(int type, CancellationToken cancellationToken)
    {
      if (await _serviceTypesService.DeleteAsync(type, cancellationToken) == 0)
      {
        return Problem(title: $"No service type found with type {type}.", statusCode: StatusCodes.Status404NotFound);
      }

      return NoContent();
    }
  }
}
