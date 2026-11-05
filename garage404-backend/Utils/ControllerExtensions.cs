using Microsoft.AspNetCore.Mvc;

namespace garage404_backend.Utils;

public static class ControllerExtensions
{
  /// <summary>
  /// Returns a 400 problem response for a malformed resource id.
  /// </summary>
  public static ObjectResult InvalidId(this ControllerBase controller, string resource) =>
    controller.Problem(title: $"{resource} id is invalid.", statusCode: StatusCodes.Status400BadRequest);

  /// <summary>
  /// Returns a 400 validation problem response for a single field.
  /// </summary>
  public static ActionResult FieldError(this ControllerBase controller, string field, string message)
  {
    controller.ModelState.AddModelError(field, message);
    return controller.ValidationProblem(controller.ModelState);
  }
}
