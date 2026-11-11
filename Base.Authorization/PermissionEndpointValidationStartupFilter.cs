using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Base.Authorization;

/// <summary>
/// Fails application startup when a route endpoint has no authorization metadata.
/// </summary>
internal sealed class PermissionEndpointValidationStartupFilter : IStartupFilter
{
    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)
    {
        return app =>
        {
            next(app);

            var endpoints = app.ApplicationServices.GetRequiredService<EndpointDataSource>().Endpoints;
            var unprotected = endpoints
                .OfType<RouteEndpoint>()
                .Where(endpoint => endpoint.Metadata.GetMetadata<IAuthorizeData>() == null
                                   && endpoint.Metadata.GetMetadata<IAllowAnonymous>() == null
                                   && endpoint.Metadata.GetMetadata<AuthorizationPolicy>() == null)
                .Select(endpoint => endpoint.DisplayName ?? endpoint.RoutePattern.RawText ?? "(unnamed endpoint)")
                .ToList();

            if (unprotected.Count > 0)
            {
                throw new InvalidOperationException(
                    "Endpoints must declare RequirePermission, [Authorize], or [AllowAnonymous]: " +
                    string.Join(", ", unprotected));
            }
        };
    }
}
