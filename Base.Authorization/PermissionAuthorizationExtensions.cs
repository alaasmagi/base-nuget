using Base.Contracts.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Base.Authorization;

/// <summary>
/// Provides dependency injection and endpoint helpers for permission-based authorization.
/// </summary>
public static class PermissionAuthorizationExtensions
{
    /// <summary>
    /// Registers permission-based authorization: <see cref="PermissionPolicyProvider"/>, a deny-by-default
    /// fallback policy, and a scoped <see cref="ICurrentUser"/>. Endpoints without authorization metadata are
    /// denied; mark exceptions with <c>[AllowAnonymous]</c> or <c>[Authorize]</c>. Authentication schemes are not
    /// configured here.
    /// </summary>
    /// <param name="services">The service collection to register authorization with.</param>
    /// <param name="configure">An optional callback that adjusts the permission authorization settings.</param>
    /// <returns>The same service collection so additional calls can be chained.</returns>
    public static IServiceCollection AddPermissionAuthorization(this IServiceCollection services,
        Action<PermissionAuthorizationOptions>? configure = null)
    {
        var options = new PermissionAuthorizationOptions();
        configure?.Invoke(options);

        services.AddHttpContextAccessor();
        services.AddAuthorization(authorization =>
        {
            authorization.FallbackPolicy = new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .RequireAssertion(_ => false)
                .Build();
        });
        services.Replace(ServiceDescriptor.Singleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>());
        services.TryAddScoped<ICurrentUser, CurrentUser>();

        if (options.ValidateEndpointsOnStartup)
        {
            services.AddTransient<IStartupFilter, PermissionEndpointValidationStartupFilter>();
        }

        return services;
    }

    /// <summary>
    /// Requires the caller to hold the <c>{module}.{action}</c> permission for the endpoint or endpoint group.
    /// </summary>
    /// <typeparam name="TBuilder">The endpoint convention builder type.</typeparam>
    /// <param name="builder">The endpoint convention builder.</param>
    /// <param name="module">The permission module, for example <c>courses</c>.</param>
    /// <param name="action">The permission action, for example <see cref="Actions.Create"/>.</param>
    /// <returns>The same builder so additional calls can be chained.</returns>
    public static TBuilder RequirePermission<TBuilder>(this TBuilder builder, string module, string action)
        where TBuilder : IEndpointConventionBuilder
    {
        return builder.RequireAuthorization(PermissionPolicyNames.For(module, action));
    }
}
