using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using VirtualBar.Api.Extensions;
using VirtualBar.Application.Common;

namespace VirtualBar.Tests.Api;

public sealed class ResultExtensionsTests
{
    private sealed class TestController : ControllerBase;

    private static (int? StatusCode, string Body) Translate<T>(Result<T> result)
    {
        var objectResult = Assert.IsAssignableFrom<ObjectResult>(result.ToActionResult(new TestController()));
        return (objectResult.StatusCode, JsonSerializer.Serialize(objectResult.Value));
    }

    [Fact]
    public void ToActionResult_WhenNotFound_Returns404WithError()
    {
        var (statusCode, body) = Translate(Result<bool>.NotFound("Bottle not found."));

        Assert.Equal(404, statusCode);
        Assert.Equal("""{"error":"Bottle not found."}""", body);
    }

    [Fact]
    public void ToActionResult_WhenForbidden_Returns403WithError()
    {
        var (statusCode, body) = Translate(Result<bool>.Forbidden("Forbidden."));

        Assert.Equal(403, statusCode);
        Assert.Equal("""{"error":"Forbidden."}""", body);
    }

    [Fact]
    public void ToActionResult_WhenConflictWithoutReason_Returns409WithErrorOnly()
    {
        var (statusCode, body) = Translate(Result<bool>.Conflict("Already exists."));

        Assert.Equal(409, statusCode);
        Assert.Equal("""{"error":"Already exists."}""", body);
    }

    [Fact]
    public void ToActionResult_WhenConflictWithReason_Returns409WithErrorAndReason()
    {
        var (statusCode, body) = Translate(
            Result<bool>.Conflict("An account with this email already exists.", AuthErrorReasons.EmailTaken));

        Assert.Equal(409, statusCode);
        Assert.Equal("""{"error":"An account with this email already exists.","reason":"EmailTaken"}""", body);
    }

    [Fact]
    public void ToActionResult_WhenValidationWithoutReason_Returns400WithErrorOnly()
    {
        var (statusCode, body) = Translate(Result<bool>.Fail("Name is required."));

        Assert.Equal(400, statusCode);
        Assert.Equal("""{"error":"Name is required."}""", body);
    }

    [Fact]
    public void ToActionResult_WhenValidationWithReason_Returns400WithErrorAndReason()
    {
        var (statusCode, body) = Translate(
            Result<bool>.Fail("Invalid email or password.", AuthErrorReasons.InvalidCredentials));

        Assert.Equal(400, statusCode);
        Assert.Equal("""{"error":"Invalid email or password.","reason":"InvalidCredentials"}""", body);
    }
}
