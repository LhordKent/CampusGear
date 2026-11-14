using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace CampusGear.IntegrationTests
{
    [Collection("Web app permissions")]
    public sealed class EndpointCompatibilityTests(WebAppFixture app)
    {
        [Fact]
        public void All_baseline_endpoints_keep_methods_routes_roles_antiforgery_and_rate_limits()
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "docs", "baseline-endpoints.json")))
            {
                directory = directory.Parent;
            }

            Assert.NotNull(directory);
            var baseline = JsonSerializer.Deserialize<List<EndpointContract>>(
                File.ReadAllText(Path.Combine(directory!.FullName, "docs", "baseline-endpoints.json"))
            )!;
            Assert.Equal(63, baseline.Count);
            var endpoints = app
                .Services.GetRequiredService<EndpointDataSource>()
                .Endpoints.OfType<RouteEndpoint>()
                .Where(endpoint => endpoint.Metadata.GetMetadata<ControllerActionDescriptor>() is not null)
                .ToArray();
            var actual = new List<string>();
            foreach (var endpoint in endpoints)
            {
                var methods = endpoint.Metadata.GetMetadata<IHttpMethodMetadata>()?.HttpMethods ?? [];
                foreach (var verb in methods.Where(method => method is "GET" or "POST"))
                {
                    actual.Add(verb + " /" + endpoint.RoutePattern.RawText!.TrimStart('/'));
                }
            }

            Assert.Equal(baseline.Select(item => item.Method + " " + item.Path).Order(), actual.Order());
            foreach (var expected in baseline)
            {
                var endpoint = Assert.Single(
                    endpoints.Where(item =>
                        "/" + item.RoutePattern.RawText!.TrimStart('/') == expected.Path
                        && item.Metadata.GetMetadata<IHttpMethodMetadata>()?.HttpMethods.Contains(expected.Method) == true
                    )
                );
                var authorization = endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>();
                var anonymous = endpoint.Metadata.GetMetadata<IAllowAnonymous>() is not null;
                Assert.Equal(expected.AllowAnonymous, anonymous);
                Assert.Equal(expected.RequireAuthenticated, !anonymous && authorization.Count > 0);
                var roles = authorization
                    .Where(item => !string.IsNullOrEmpty(item.Roles))
                    .Select(item => string.Join(",", item.Roles!.Split(',').Order()))
                    .Order();
                Assert.Equal(expected.Roles.Order(), roles);
                var action = endpoint.Metadata.GetMetadata<ControllerActionDescriptor>()!;
                var validatesForm =
                    expected.Method == "POST"
                    && action.FilterDescriptors.Any(filter =>
                        filter.Filter is AutoValidateAntiforgeryTokenAttribute or ValidateAntiForgeryTokenAttribute
                    );
                Assert.Equal(expected.Antiforgery, validatesForm);
                Assert.Equal(expected.RateLimit, endpoint.Metadata.GetMetadata<EnableRateLimitingAttribute>()?.PolicyName);
            }
        }

        private sealed record EndpointContract(
            string Method,
            string Path,
            bool RequireAuthenticated,
            bool AllowAnonymous,
            string[] Roles,
            bool Antiforgery,
            string? RateLimit
        );
    }
}
