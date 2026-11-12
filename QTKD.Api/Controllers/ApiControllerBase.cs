using Microsoft.AspNetCore.Mvc;
using QTKD.Api.Common;

namespace QTKD.Api.Controllers;

public abstract class ApiControllerBase : ControllerBase
{
    /// <summary>
    /// Maps a business error code to its HTTP status. Every error body uses
    /// <see cref="ApiErrorResponse"/> so clients can read <c>code</c>/<c>message</c>/<c>details</c>.
    /// </summary>
    protected ActionResult<T> ToErrorResult<T>(ApiErrorResponse error) => error.Code switch
    {
        ApiErrorCodes.NotFound => NotFound(error),
        ApiErrorCodes.Conflict => Conflict(error),
        ApiErrorCodes.SubmissionValidationFailed => UnprocessableEntity(error),
        _ => BadRequest(error)
    };

    protected NotFoundObjectResult NotFoundError(string resource, int id) =>
        NotFound(new ApiErrorResponse(ApiErrorCodes.NotFound, $"{resource} {id} was not found."));
}
