using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using VirtualBar.HttpTests.Support;

namespace VirtualBar.HttpTests.Access;

/// <summary>
/// The access rule of every controller endpoint, read from the running app (#68). Which endpoints a visitor without
/// a token may call is a decision, so it is written out in <see cref="AnonymousEndpoints"/>: adding or removing an
/// <c>[AllowAnonymous]</c>, or forgetting <c>[Authorize]</c> on a new controller, fails here until the list says the
/// same. Every other endpoint must refuse a request without a token with 401, before binding or a lookup can answer.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed partial class EndpointAccessTests(ApiHosts hosts)
{
    /// <summary>Every endpoint a visitor may call without a token, as <c>METHOD route template</c>.</summary>
    private static readonly string[] AnonymousEndpoints =
    [
        // Getting a token.
        "POST api/Auth/register",
        "POST api/Auth/login",
        "POST api/Auth/confirm-email",
        "POST api/Auth/resend-confirmation",
        "POST api/Auth/forgot-password",
        "POST api/Auth/reset-password",

        // Public bars: collectors, their bottles and what others say about them.
        "GET api/users",
        "GET api/users/{userId:guid}",
        "GET api/users/{userId:guid}/followers",
        "GET api/users/{userId:guid}/following",
        "GET api/badges/user/{userId:guid}",
        "GET api/Bottles",
        "GET api/Bottles/{id:guid}",
        "GET api/Bottles/marketplace",
        "GET api/bottles/{bottleId:guid}/comments",
        "GET api/bottles/{bottleId:guid}/reviews",

        // The home page.
        "GET api/Feed",
        "GET api/News",
        "GET api/News/{id:guid}",

        // The catalog and the add-bottle pickers.
        "GET api/Products",
        "GET api/Products/browse",
        "GET api/Products/{id:guid}",
        "GET api/Products/{id:guid}/reviews",
        "GET api/distilleries",
    ];

    // Route parameters get a value that matches their constraint and names nothing in the database.
    private static readonly Guid PlaceholderId = Guid.NewGuid();

    private ApiFactory Api => hosts.ConfirmationOff;

    [SqlServerFact]
    public async Task AnonymousEndpoints_AreExactlyTheListedOnes()
    {
        var anonymous = (await ControllerEndpointsAsync())
            .Where(endpoint => endpoint.IsAnonymous)
            .Select(endpoint => endpoint.Name)
            .ToList();

        var notListed = anonymous.Except(AnonymousEndpoints, StringComparer.OrdinalIgnoreCase).Order().ToList();
        var notAnonymous = AnonymousEndpoints.Except(anonymous, StringComparer.OrdinalIgnoreCase).Order().ToList();

        Assert.True(
            notListed.Count == 0 && notAnonymous.Count == 0,
            "The anonymous endpoints differ from AnonymousEndpoints. Make the attribute and the list agree, deliberately." +
            $"{Environment.NewLine}Anonymous but not listed: {string.Join(", ", notListed)}" +
            $"{Environment.NewLine}Listed but not anonymous: {string.Join(", ", notAnonymous)}");
    }

    [SqlServerFact]
    public async Task ProtectedEndpoints_WhenThereIsNoToken_Return401()
    {
        var client = Api.CreateClient();
        var endpoints = (await ControllerEndpointsAsync()).Where(endpoint => !endpoint.IsAnonymous).ToList();
        Assert.NotEmpty(endpoints);

        var wrongAnswers = new List<string>();
        foreach (var endpoint in endpoints)
        {
            var response = await client.SendAsync(endpoint.Request());
            if (response.StatusCode != HttpStatusCode.Unauthorized)
                wrongAnswers.Add($"{endpoint.Name} answered {(int)response.StatusCode}");
        }

        Assert.Empty(wrongAnswers);
    }

    [SqlServerFact]
    public async Task AnonymousEndpoints_WhenThereIsNoToken_AnswerWithout401OrAServerError()
    {
        var client = Api.CreateClient();
        var endpoints = (await ControllerEndpointsAsync()).Where(endpoint => endpoint.IsAnonymous).ToList();
        Assert.NotEmpty(endpoints);

        var wrongAnswers = new List<string>();
        foreach (var endpoint in endpoints)
        {
            var response = await client.SendAsync(endpoint.Request());
            if (response.StatusCode == HttpStatusCode.Unauthorized || (int)response.StatusCode >= 500)
                wrongAnswers.Add($"{endpoint.Name} answered {(int)response.StatusCode}");
        }

        Assert.Empty(wrongAnswers);
    }

    /// <summary>
    /// The controller actions only. In Development the app also maps the OpenAPI document and the Scalar page, which
    /// are anonymous and never exist outside Development, so they are not part of the API's access rules.
    /// </summary>
    private async Task<List<ControllerEndpoint>> ControllerEndpointsAsync()
    {
        var services = Api.Services;
        // With no fallback policy, an endpoint that carries no authorization metadata at all is public.
        var fallbackPolicy = await services.GetRequiredService<IAuthorizationPolicyProvider>().GetFallbackPolicyAsync();

        return services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>()
            .Where(endpoint => endpoint.Metadata.GetMetadata<ControllerActionDescriptor>() is not null)
            .SelectMany(endpoint => endpoint.Metadata.GetRequiredMetadata<IHttpMethodMetadata>().HttpMethods
                .Select(method => new ControllerEndpoint(
                    method,
                    endpoint.RoutePattern.RawText!,
                    IsAnonymous: endpoint.Metadata.GetMetadata<IAllowAnonymous>() is not null
                        || (fallbackPolicy is null && endpoint.Metadata.GetMetadata<IAuthorizeData>() is null),
                    Consumes: endpoint.Metadata.GetMetadata<IAcceptsMetadata>()?.ContentTypes ?? [])))
            .ToList();
    }

    private sealed record ControllerEndpoint(string Method, string Route, bool IsAnonymous, IReadOnlyList<string> Consumes)
    {
        public string Name => $"{Method} {Route}";

        /// <summary>
        /// A request routing sends to this endpoint. A body goes with every method that takes one, in the content
        /// type the endpoint accepts: routing checks it before authentication runs, so an upload endpoint
        /// (<c>[Consumes]</c> or an <c>IFormFile</c> parameter) answers a JSON body with 415 instead of 401.
        /// </summary>
        public HttpRequestMessage Request()
        {
            var request = new HttpRequestMessage(new HttpMethod(Method), "/" + RouteParameter().Replace(Route, Placeholder));

            if (Method is "POST" or "PUT" or "PATCH")
                request.Content = Consumes.Any(type => type.StartsWith("multipart/", StringComparison.OrdinalIgnoreCase))
                    ? new MultipartFormDataContent()
                    : new StringContent("{}", Encoding.UTF8, "application/json");

            return request;
        }

        private static string Placeholder(Match parameter) =>
            parameter.Groups["constraints"].Value.Contains(":guid", StringComparison.OrdinalIgnoreCase)
                ? PlaceholderId.ToString()
                : "placeholder";
    }

    [GeneratedRegex(@"\{(?<name>[^}:]+)(?<constraints>[^}]*)\}")]
    private static partial Regex RouteParameter();
}
