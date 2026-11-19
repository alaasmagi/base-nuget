using System.Security.Claims;
using Base.Authorization;
using Base.Keycloak.Authentication;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

public class PermissionMappingTests
{
    private const string ResourceAccess = """
        {
          "educode-api": { "roles": ["course-management", "courses.create", "courses.edit", "courses.edit.all", ".x", "x."] },
          "other-api": { "roles": ["reports.create", "report-management"] }
        }
        """;

    private static KeycloakOptions Options() => new()
    {
        Authority = "https://identity.example.test/realms/educode",
        ClientId = "educode-api"
    };

    private static ClaimsPrincipal Principal(string resourceAccess) => new(new ClaimsIdentity(
        [
            new Claim("sub", "3f2c9a1e-6a0e-4c4f-9f0e-2d8b6f1a0b11"),
            new Claim("resource_access", resourceAccess),
            new Claim("realm_access", """{ "roles": ["offline_access", "uma_authorization"] }""")
        ],
        "test"));

    private static async Task<string[]> MapAsync(string resourceAccess)
    {
        var principal = await new KeycloakPermissionClaimsTransformation(Options())
            .TransformAsync(Principal(resourceAccess));
        return principal.FindAll(PermissionClaimTypes.Permission).Select(c => c.Value).Order().ToArray();
    }

    [Fact]
    public async Task MapsOnlyOwnClientPermissionsWithExactlyOneDot()
    {
        Assert.Equal(["courses.create", "courses.edit"], await MapAsync(ResourceAccess));
    }

    [Fact]
    public async Task AddsNoRoleClaims()
    {
        var principal = await new KeycloakPermissionClaimsTransformation(Options())
            .TransformAsync(Principal(ResourceAccess));

        Assert.Empty(principal.FindAll(ClaimTypes.Role));
    }

    [Theory]
    [InlineData("""{ "other-api": { "roles": ["courses.create"] } }""")]
    [InlineData("""{ "educode-api": { "roles": ["course-management"] } }""")]
    [InlineData("not json")]
    public async Task MapsNothingWithoutOwnClientPermissions(string resourceAccess)
    {
        Assert.Empty(await MapAsync(resourceAccess));
    }

    [Fact]
    public void JwtBearerAlwaysRegistersPermissionTransformationAndRawClaimNames()
    {
        var services = new ServiceCollection();
        services.AddKeycloakJwtBearer(Options());
        using var provider = services.BuildServiceProvider();

        Assert.IsType<KeycloakPermissionClaimsTransformation>(
            Assert.Single(provider.GetServices<IClaimsTransformation>()));
        Assert.False(provider.GetRequiredService<IOptionsMonitor<JwtBearerOptions>>()
            .Get(JwtBearerDefaults.AuthenticationScheme).MapInboundClaims);
    }

    [Fact]
    public void OidcAlwaysRegistersPermissionTransformationAndCookiePermissionMapping()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddKeycloakOidc(Options());
        using var provider = services.BuildServiceProvider();

        Assert.IsType<KeycloakPermissionClaimsTransformation>(
            Assert.Single(provider.GetServices<IClaimsTransformation>()));
        var cookie = provider.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>()
            .Get(CookieAuthenticationDefaults.AuthenticationScheme);
        Assert.Equal("PermissionClaimsStrippingTicketDataFormat", cookie.TicketDataFormat.GetType().Name);
    }

    [Fact]
    public void NameIsTheRawNameClaimForJwtAndOidc()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddKeycloakOidc(Options()).AddKeycloakJwtBearer(Options());
        using var provider = services.BuildServiceProvider();

        Assert.Equal("name", provider.GetRequiredService<IOptionsMonitor<JwtBearerOptions>>()
            .Get(JwtBearerDefaults.AuthenticationScheme).TokenValidationParameters.NameClaimType);
        Assert.Equal("name", provider.GetRequiredService<IOptionsMonitor<OpenIdConnectOptions>>()
            .Get(OpenIdConnectDefaults.AuthenticationScheme).TokenValidationParameters.NameClaimType);
    }

    [Fact]
    public void MapInboundClaimsDefaultsToFalse()
    {
        Assert.False(Options().MapInboundClaims);
    }
}
