using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Base.Keycloak.Authentication;

/// <summary>
/// Provides dependency injection helpers for Keycloak JWT bearer authentication.
/// </summary>
public static class KeycloakJwtExtensions
{
    /// <summary>
    /// Registers JWT bearer authentication configured for Keycloak as the default authentication scheme.
    /// </summary>
    /// <param name="services">The service collection to register authentication with.</param>
    /// <param name="options">The Keycloak authentication settings.</param>
    /// <returns>The authentication builder created by the registration.</returns>
    public static AuthenticationBuilder AddKeycloakJwtBearer(this IServiceCollection services, KeycloakOptions options)
    {
        services.AddSingleton(options);
        services.AddKeycloakClaimsTransformation(options);

        return services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddKeycloakJwtBearer(options);
    }

    /// <summary>
    /// Adds Keycloak JWT bearer authentication to an existing authentication builder without changing its default
    /// schemes, for example next to <see cref="KeycloakOidcExtensions.AddKeycloakOidc"/> so one host serves both
    /// cookie-based pages and bearer-token APIs.
    /// </summary>
    /// <param name="builder">The authentication builder to add the JWT bearer scheme to.</param>
    /// <param name="options">The Keycloak authentication settings.</param>
    /// <returns>The same authentication builder so additional calls can be chained.</returns>
    public static AuthenticationBuilder AddKeycloakJwtBearer(this AuthenticationBuilder builder, KeycloakOptions options)
    {
        builder.Services.TryAddSingleton(options);
        builder.Services.TryAdd(KeycloakClaimsTransformationDescriptor(options));

        return builder.AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, jwtOptions =>
        {
            jwtOptions.Authority = options.Authority;
            jwtOptions.Audience = string.IsNullOrEmpty(options.Audience) ? options.ClientId : options.Audience;
            jwtOptions.RequireHttpsMetadata = options.RequireHttpsMetadata;
            jwtOptions.MapInboundClaims = options.MapInboundClaims;
            jwtOptions.TokenValidationParameters.ValidateAudience = options.ValidateAudience;

            // Platform token contract: JWKS signature, exact issuer from the authority metadata, and lifetime.
            jwtOptions.TokenValidationParameters.ValidateIssuer = true;
            jwtOptions.TokenValidationParameters.ValidateIssuerSigningKey = true;
            jwtOptions.TokenValidationParameters.RequireSignedTokens = true;
            jwtOptions.TokenValidationParameters.ValidateLifetime = true;
            jwtOptions.TokenValidationParameters.RequireExpirationTime = true;
            jwtOptions.TokenValidationParameters.ClockSkew = options.ClockSkew;

            if (options.NameClaimType != null)
            {
                jwtOptions.TokenValidationParameters.NameClaimType = options.NameClaimType;
            }

            if (options.RoleClaimType != null)
            {
                jwtOptions.TokenValidationParameters.RoleClaimType = options.RoleClaimType;
            }
        });
    }

    internal static void AddKeycloakClaimsTransformation(this IServiceCollection services, KeycloakOptions options)
    {
        services.Add(KeycloakClaimsTransformationDescriptor(options));
    }

    private static ServiceDescriptor KeycloakClaimsTransformationDescriptor(KeycloakOptions options)
    {
        return options.MapPermissions
            ? ServiceDescriptor.Transient<IClaimsTransformation, KeycloakPermissionClaimsTransformation>()
            : ServiceDescriptor.Transient<IClaimsTransformation, KeycloakRoleClaimsTransformation>();
    }
}
