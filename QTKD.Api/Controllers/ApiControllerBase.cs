using Microsoft.AspNetCore.Mvc;
using QTKD.Api.Common;

namespace QTKD.Api.Controllers;

public abstract class ApiControllerBase : ControllerBase
{
    protected ActionResult<T> ToErrorResult<T>(ApiErrorResponse error) => error.Code switch
    {
        "not_found" => NotFound(error),
        "conflict" => Conflict(error),
        "submission_validation_failed" => UnprocessableEntity(error),
        _ => BadRequest(error)
    };
}
