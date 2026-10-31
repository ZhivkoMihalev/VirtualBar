using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace VirtualBar.HttpTests.Support;

/// <summary>The auth endpoints as the frontend calls them, plus the checks every auth test repeats.</summary>
internal static class AuthApi
{
    /// <summary>Meets the password rules (8+ characters, an uppercase letter, a lowercase letter and a digit).</summary>
    public const string Password = "HttpTests-Passw0rd";

    /// <summary>An address no other test, and no earlier run, has used.</summary>
    public static string NewEmail(string purpose) => $"{purpose}-{Guid.NewGuid():N}@httptests.virtualbar.test";

    public static Task<HttpResponseMessage> RegisterAsync(this HttpClient client, string email, string password = Password) =>
        client.PostAsJsonAsync("/api/auth/register", new { email, password, displayName = "HTTP test collector" });

    public static Task<HttpResponseMessage> LoginAsync(this HttpClient client, string email, string password = Password) =>
        client.PostAsJsonAsync("/api/auth/login", new { email, password });

    public static Task<HttpResponseMessage> ConfirmEmailAsync(this HttpClient client, string email, string token) =>
        client.PostAsJsonAsync("/api/auth/confirm-email", new { email, token });

    public static Task<HttpResponseMessage> ResendConfirmationAsync(this HttpClient client, string email) =>
        client.PostAsJsonAsync("/api/auth/resend-confirmation", new { email });

    public static void UseToken(this HttpClient client, string token) =>
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

    /// <summary>
    /// The body as JSON. <see cref="JsonElement.GetProperty(string)"/> is case-sensitive, so reading through it also
    /// pins the camelCase names the frontend reads.
    /// </summary>
    public static async Task<JsonElement> JsonAsync(this HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<JsonElement>();

    /// <summary>
    /// The 200 answer register gives with email confirmation on, checked in full: it asks the user to confirm their
    /// email, issues no token, carries no user and nothing else. A new email and an email that already has an account
    /// both get exactly this, so the answer does not reveal which emails are registered (#66).
    /// </summary>
    public static async Task AssertConfirmationRequestedAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.JsonAsync();
        Assert.Equal(
            new[] { "requiresEmailConfirmation", "token", "user" },
            body.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal));
        Assert.True(body.GetProperty("requiresEmailConfirmation").GetBoolean());
        Assert.Equal("", body.GetProperty("token").GetString());
        Assert.Equal(JsonValueKind.Null, body.GetProperty("user").ValueKind);
    }

    /// <summary>A 200 auth response with a token, which it returns.</summary>
    public static async Task<string> AssertTokenIssuedAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var token = (await response.JsonAsync()).GetProperty("token").GetString();
        Assert.False(string.IsNullOrEmpty(token));
        return token!;
    }

    /// <summary>An error in the <c>{ error, reason }</c> shape the frontend translates by <paramref name="reason"/>.</summary>
    public static async Task AssertErrorAsync(HttpResponseMessage response, HttpStatusCode status, string reason)
    {
        Assert.Equal(status, response.StatusCode);
        var body = await response.JsonAsync();
        Assert.Equal(reason, body.GetProperty("reason").GetString());
        Assert.False(string.IsNullOrWhiteSpace(body.GetProperty("error").GetString()));
    }
}
