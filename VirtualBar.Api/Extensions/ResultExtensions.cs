using Microsoft.AspNetCore.Mvc;
using VirtualBar.Application.Common;

namespace VirtualBar.Api.Extensions;

public static class ResultExtensions
{
    public static IActionResult ToActionResult<T>(this Result<T> result, ControllerBase controller) =>
        result.ErrorCode switch
        {
            ErrorCode.NotFound  => controller.NotFound(new { error = result.Error }),
            ErrorCode.Forbidden => controller.StatusCode(StatusCodes.Status403Forbidden, new { error = result.Error }),
            ErrorCode.Conflict  => controller.Conflict(new { error = result.Error }),
            _                   => controller.BadRequest(new { error = result.Error })
        };
}
