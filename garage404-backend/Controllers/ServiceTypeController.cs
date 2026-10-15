using garage404_backend.Models;
using garage404_backend.Models.Utils;
using garage404_backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace garage404_backend.Controllers
{
  [Route("v1/[controller]")]
  [ApiController]
  [Authorize]
  public class ServiceTypeController(ServiceTypesService serviceTypesService, UsersService usersService, ILogger<ServiceTypeController> logger) : ControllerBase
  {
    private readonly ServiceTypesService _serviceTypesService = serviceTypesService;
    private readonly UsersService _usersService = usersService;
    private readonly ILogger<ServiceTypeController> _logger = logger;

    [HttpPost]
    [Consumes("application/json")]
    public async Task<IActionResult> AddServiceTypes([FromBody] List<ServiceType> json)
    {
      if (json == null || json.Count == 0)
      {
        return BadRequest("No service types provided.");
      }

      if (!await Authentication.IsAdminAsync(User, _usersService))
      {
        return Forbid();
      }

      try
      {
        await _serviceTypesService.CreateAsync(json);
      }
      catch (Exception e)
      {
        _logger.LogError(e, "Error adding service types");
        return BadRequest();
      }

      return Ok();
    }

    [HttpGet]
    public async Task<IActionResult> GetServiceTypes()
    {
      try
      {
        var serviceTypes = await _serviceTypesService.GetAsync();
        return Ok(serviceTypes);
      }
      catch (Exception e)
      {
        _logger.LogError(e, "Error fetching service types");
        return BadRequest();
      }
    }

    [HttpDelete("{type}")]
    public async Task<IActionResult> DeleteServiceType(int type)
    {
      if (!await Authentication.IsAdminAsync(User, _usersService))
      {
        return Forbid();
      }

      try
      {
        var serviceTypes = await _serviceTypesService.GetAsync(type);
        if (serviceTypes.Count == 0)
        {
          return NotFound($"No service types found with type {type}");
        }

        foreach (var serviceType in serviceTypes)
        {
          await _serviceTypesService.DeleteAsync(serviceType.Id);
        }
      }
      catch (Exception e)
      {
        _logger.LogError(e, "Error deleting service type with type {type}", type);
        return BadRequest();
      }

      return Ok();
    }
  }
}
