# alaasmagi Base NuGet Packages

Reusable base-layer packages for .NET applications built around a layered architecture.

This repository contains the source for the `alaasmagi.Base.*` package set. The packages provide shared contracts and base implementations for:

- domain entities
- ASP.NET Core Identity base entities
- DTO and mapper abstractions
- method-response and error models
- repositories and unit of work
- EF Core data access
- application services
- typed exceptions
- key-value cache contracts and base implementations
- event messaging contracts and helpers
- RabbitMQ event transport support
- Keycloak authentication, admin API models, and identity event payloads
- permission-based authorization for Keycloak-secured APIs

## Packages

The repository currently contains these NuGet packages:

- `alaasmagi.Base.Contracts.Domain`
- `alaasmagi.Base.Domain`
- `alaasmagi.Base.Domain.Identity`
- `alaasmagi.Base.Contracts.DTO`
- `alaasmagi.Base.DTO`
- `alaasmagi.Base.Contracts.DataAccess`
- `alaasmagi.Base.DataAccess.EF`
- `alaasmagi.Base.Contracts.Application`
- `alaasmagi.Base.Application`
- `alaasmagi.Base.Contracts.Exception`
- `alaasmagi.Base.Exception`
- `alaasmagi.Base.Contracts.Cache`
- `alaasmagi.Base.Cache`
- `alaasmagi.Base.Contracts.Message`
- `alaasmagi.Base.Message`
- `alaasmagi.Base.Message.RabbitMQ`
- `alaasmagi.Base.Keycloak`
- `alaasmagi.Base.Contracts.Authorization`
- `alaasmagi.Base.Authorization`

All projects currently target `.NET 10.0` and declare version `1.6.0`.

## What Each Package Does

### Domain

- `Base.Contracts.Domain` defines entity contracts such as `IBaseEntity<TKey>`, `IBaseEntityMeta`, `IBaseEntitySoftDelete`, and `IBaseEntityUserId<TKey>`.
- `Base.Domain` provides base classes such as `BaseEntity<TKey>`, `BaseEntityWithMeta<TKey>`, `BaseEntityWithMetaSoftDelete<TKey>`, `BaseEntityUserWithMeta<TKey, TUserKey>`, and `BaseEntityUserWithMetaSoftDelete<TKey, TUserKey>`.
- `Base.Domain.Identity` provides ASP.NET Core Identity user and role base classes with metadata, soft delete, and concurrency variants.

### DTO

- `Base.Contracts.DTO` defines `IMapper<...>`, `IMethodResponse<...>`, and `IError<...>`.
- `Base.DTO` provides `MethodResponse`, `Error`, and shared `ErrorDefaults`.

### Data Access

- `Base.Contracts.DataAccess` defines repository contracts and `IBaseUow`.
- `Base.DataAccess.EF` provides generic EF Core implementations for repository and unit-of-work patterns, including soft delete support.

### Application

- `Base.Contracts.Application` defines generic CRUD and soft-delete service contracts.
- `Base.Application` provides generic application service implementations that map between application models and repository/domain models.

### Exceptions

- `Base.Contracts.Exception` defines `IBaseException` and `IHttpException`.
- `Base.Exception` provides `BaseException` and `HttpException`.

### Caching

- `Base.Contracts.Cache` defines key-value cache, cache result, entry options, serializer, and key-builder contracts.
- `Base.Cache` provides provider-neutral cache results, entry options, key building, JSON serialization, and a byte-backed `BaseCache` abstraction for Redis or other cache backends.

### Messaging

- `Base.Contracts.Message` defines event-envelope, publisher, and handler contracts.
- `Base.Message` provides base event envelopes, default source names, routing-key validation, notification recipient content, and command expiry support.
- `Base.Message.RabbitMQ` provides RabbitMQ options, connection management, event publishing, listener base support, and publisher registration helpers.

### Keycloak

- `Base.Keycloak` provides JWT bearer and OpenID Connect registration helpers for Keycloak, configurable through `KeycloakOptions` and combinable in one host.
- `Base.Keycloak` also provides role claim transformation, admin API models/client helpers, and identity lifecycle/action event payloads.
- With `KeycloakOptions.MapPermissions`, `KeycloakPermissionClaimsTransformation` turns `resource_access.{clientId}.roles` values containing a dot into `permission` claims for `Base.Authorization`.
- JWT bearer validates the JWKS signature, the exact issuer from the authority metadata, the audience (`Audience`, falling back to `ClientId`), and token lifetime with `ClockSkew` (default 30 seconds).

### Authorization

- `Base.Contracts.Authorization` defines `ICurrentUser` (`IsAuthenticated`, `Id` from `sub`, display-only `Email`/`Name`, `IsService`, `ClientId`, `Permissions`, `HasPermission`).
- `Base.Authorization` provides `Actions`, `RequirePermissionAttribute`, `PermissionPolicyProvider` (`perm:{module}.{action}` policies), the minimal API `RequirePermission(...)` extension, `CurrentUser`, and `AddPermissionAuthorization(...)` with a deny-by-default fallback policy. It only reads `permission` claims and does not depend on Keycloak.

## Typical Flow

The package set is designed for a layered setup:

1. Define a domain model by inheriting from one of the `Base.Domain` base classes.
2. Define a persistence model if your EF entity differs from the domain model.
3. Implement an `IMapper<TUpperEntity, TLowerEntity, TKey>` for the repository layer.
4. Inherit from `BaseRepository<...>` or `BaseRepositorySoftDelete<...>`.
5. If your application layer exposes different models, implement another mapper between application models and domain models.
6. Inherit from `BaseService<...>` or `BaseServiceSoftDelete<...>`.

## Example

```csharp
using Base.Contracts.DTO;
using Base.DataAccess.EF;
using Base.Domain;
using Microsoft.EntityFrameworkCore;

public class Todo : BaseEntityWithMetaSoftDelete<Guid>
{
    public string Title { get; set; } = default!;
}

public class TodoDbEntity : BaseEntityWithMetaSoftDelete<Guid>
{
    public string Title { get; set; } = default!;
}

public class TodoMapper : IMapper<Todo, TodoDbEntity, Guid>
{
    public Todo? Map(TodoDbEntity? entity) => entity == null
        ? null
        : new Todo
        {
            Id = entity.Id,
            Title = entity.Title,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy,
            IsDeleted = entity.IsDeleted
        };

    public IEnumerable<Todo>? Map(IEnumerable<TodoDbEntity>? entities) =>
        entities?.Select(Map)!;

    public TodoDbEntity? Map(Todo? entity) => entity == null
        ? null
        : new TodoDbEntity
        {
            Id = entity.Id,
            Title = entity.Title,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy,
            IsDeleted = entity.IsDeleted
        };

    public IEnumerable<TodoDbEntity>? Map(IEnumerable<Todo>? entities) =>
        entities?.Select(Map)!;
}

public class TodoRepository
    : BaseRepositorySoftDelete<Todo, TodoDbEntity, TodoMapper, Guid, Guid>
{
    public TodoRepository(DbContext dbContext, TodoMapper mapper)
        : base(dbContext, mapper)
    {
    }
}
```

## Permission authorization

Permissions are Keycloak client roles named `{module}.{action}` on the backend's own client. Code checks
permissions only, never roles. Endpoints without authorization metadata are denied.

```csharp
using Base.Authorization;
using Base.Contracts.Authorization;
using Base.Keycloak.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

// Registration (Program.cs)
builder.Services.AddKeycloakJwtBearer(new KeycloakOptions
{
    Authority = "https://identity.alaasmagi.dev/realms/someRealm",
    Audience = "someApp-api",   // own clientId: required in "aud", key under resource_access
    MapPermissions = true       // resource_access.someApp-api.roles -> "permission" claims
});
builder.Services.AddPermissionAuthorization(options => options.ValidateEndpointsOnStartup = true);

var app = builder.Build();
app.UseAuthentication();
app.UseAuthorization();

// Minimal API
app.MapDelete("/api/courses/{id:guid}", (Guid id, ICurrentUser user) => Results.NoContent())
    .RequirePermission("courses", Actions.Delete);

// App-specific actions; the standard constants stay available as Actions.Create etc.
public sealed class AppActions : Actions
{
    public const string Mark = "mark";
}

// Controller
[ApiController]
[Route("api/courses")]
public class CoursesController(ICurrentUser currentUser) : ControllerBase
{
    [HttpGet]
    [RequirePermission("courses", Actions.View)]
    public IActionResult List() => currentUser.HasPermission("courses", Actions.ViewAll)
        ? Ok("all courses")
        : Ok($"courses of {currentUser.Id}");

    [HttpPost("{id:guid}/grades")]
    [RequirePermission("grades", AppActions.Mark)]
    public IActionResult Mark(Guid id) => NoContent();

    [HttpGet("catalog")]
    [AllowAnonymous]
    public IActionResult Catalog() => currentUser.IsAuthenticated
        ? Ok($"catalog for {currentUser.Id}")
        : Ok("public catalog");
}
```

- `ICurrentUser.Id` is the token `sub` and is the only user identifier; `Email` and `Name` are display-only.
- `Id` throws `InvalidOperationException` when there is no valid `sub`. On `[AllowAnonymous]` endpoints check
  `IsAuthenticated` before reading `Id`.
- Service tokens (client credentials) have `IsService == true` and `ClientId` set; their permissions come from
  the service account's client roles in the same way.
- Use `[Authorize]` for endpoints that only need a signed-in caller and `[AllowAnonymous]` for public ones.

## Notes

- Package versions should be kept aligned across the full `alaasmagi.Base.*` set.
- `Base.DataAccess.EF` expects Entity Framework Core.
- Metadata fields such as `CreatedAt`, `UpdatedAt`, `CreatedBy`, and `UpdatedBy` are managed by the EF repository base classes when the entity supports metadata.
- User scoping is applied automatically when the entity implements `IBaseEntityUserId<TActor>` and a non-default actor value is provided.
- Soft-delete operations are available through the `SoftDelete` repository and service variants.
- Cache packages define a provider-neutral abstraction; concrete Redis or other store implementations can inherit from `BaseCache`.
- Messaging packages provide contracts and reusable RabbitMQ infrastructure for topic-based event workflows.
- Keycloak helpers are intended for ASP.NET Core applications that need Keycloak authentication, role claims, admin user operations, or identity event payloads.
- Authorization packages check `permission` claims only. `AddPermissionAuthorization` does not configure authentication schemes.


## Platform notification contract

The authoritative examples are in `docs/contracts/event-contract.json`. Values in square brackets are
placeholders, not literal wire values. Use `contentVersion: "1.0"`.

- Publish domain events to `identity-hub.events` and notification commands to
  `notification-hub.commands`. Both are durable topic exchanges in vhost `/`, managed externally.
  Configure `RabbitMqOptions.Exchange`; the packages do not declare exchanges or queues.
- Use `DefaultSources.IdentityHub` for Keycloak and `DefaultSources.NotificationHub` for the notification
  service. Apps use their existing slug for both source and tenant; Keycloak uses the realm as tenant.
- Derive routing keys only through `BaseRoutingKey.For(envelope)`. Routing keys never belong in the body.
- Command content inherits `BaseNotificationContent`: `UserId` (UUID string), `Email`, `FullName`,
  `Locale` (BCP 47 including region, e.g. `et-EE`), and `TimeZone` (IANA, e.g. `Europe/Tallinn`).
  Publish one command per recipient. Event content does not inherit this base.
- `Locale` and `TimeZone` retain nullable setters for source compatibility, but both must be populated
  for commands. The RabbitMQ publisher validates recipient metadata before contacting the broker.
- `BaseEventEnvelope<T>.Create(...)` preserves the existing signature and copies expiry from
  `IExpiringNotificationContent`, implemented by the three existing Keycloak command DTOs.
  For other commands use `CreateCommand(source, tenant, action, "1.0", content, expiresAt)`.
  Invoice expiry is null; lunch expiry is the end of today's latest offer, converted to UTC.
- Events omit `expiresAt`; commands write it even when null. The envelope converter preserves this
  distinction with default System.Text.Json settings and with null omission enabled. Missing expiry
  deserializes as null. When constructing Keycloak envelopes directly, set envelope expiry to the
  payload expiry; the publisher rejects mismatches. Broker TTL remains a separate transport option.
- Existing `VerifyEmailContent`, `OtpEmailContent`, and `PasswordResetContent` names remain supported.
  `UserEventContent` omits absent optional properties; use only `UserId` for `user-deleted`.
- Send integer fields (`validForMinutes`, `fractions`) as JSON numbers, money as invariant decimal
  strings with a separate currency, dates as `yyyy-MM-dd`, and times of day as `HH:mm`.
- Never log or persist notification content such as OTP codes or action links.

Run contract tests with `dotnet test tests/Base.Message.Tests/Base.Message.Tests.csproj -c Release`.
Run authorization tests with `dotnet test tests/Base.Authorization.Tests/Base.Authorization.Tests.csproj -c Release`.
