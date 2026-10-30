using Microsoft.AspNetCore.Mvc;
using VirtualBar.Application.Common;

namespace VirtualBar.Api.Extensions;

public static class ResultExtensions
{
    public static IActionResult ToActionResult<T>(this Result<T> result, ControllerBase controller)
    {
        // The reason is added only when the service set one, so every other error keeps its { error } shape.
        var body = result.Reason is null
            ? (object)new { error = result.Error }
            : new { error = result.Error, reason = result.Reason };

        return result.ErrorCode switch
        {
            ErrorCode.NotFound  => controller.NotFound(body),
            ErrorCode.Forbidden => controller.StatusCode(StatusCodes.Status403Forbidden, body),
            ErrorCode.Conflict  => controller.Conflict(body),
            _                   => controller.BadRequest(body)
        };
    }
}
