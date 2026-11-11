# alaasmagi Base Packages

Reusable base-layer building blocks for .NET applications.

This package set provides shared contracts and generic implementations for common layers:

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

## Included Capabilities

These packages cover the common building blocks used across layered applications:

- strongly typed base entity contracts
- domain base classes with metadata, ownership, and soft delete support
- mapper abstractions between layers
- method-response and error models
- generic repository contracts
- generic EF Core repository implementations
- unit-of-work abstractions
- generic application service contracts and implementations
- typed base exceptions and HTTP exceptions
- key-value cache contracts and provider-neutral base implementations
- event-envelope, publisher, and handler contracts
- RabbitMQ event publishing and listener infrastructure
- Keycloak authentication helpers, admin API models, and identity event payloads
- permission-based authorization with deny-by-default endpoints

## Package Overview

### `alaasmagi.Base.Contracts.Domain`

Defines contracts such as:

- `IBaseEntity<TKey>`
- `IBaseEntityMeta`
- `IBaseEntitySoftDelete`
- `IBaseEntityUserId<TKey>`
- `IBaseEntityWithMeta<TKey>`
- `IBaseEntityWithMetaSoftDelete<TKey>`

### `alaasmagi.Base.Domain`

Provides reusable base entity types such as:

- `BaseEntity<TKey>`
- `BaseEntityWithMeta<TKey>`
- `BaseEntityWithMetaSoftDelete<TKey>`
- `BaseEntityUser<TKey>`
- `BaseEntityUserWithMeta<TKey, TUserKey>`
- `BaseEntityUserWithMetaSoftDelete<TKey, TUserKey>`

### `alaasmagi.Base.Domain.Identity`

Provides ASP.NET Core Identity base types such as:

- `BaseIdentityUser<TKey>`
- `BaseIdentityUserWithMeta<TKey>`
- `BaseIdentityUserWithMetaSoftDelete<TKey>`
- `BaseIdentityUserWithMetaSoftDeleteConcurrency<TKey>`
- `BaseIdentityRole<TKey>`
- `BaseIdentityRoleWithMeta<TKey>`
- `BaseIdentityRoleWithMetaSoftDelete<TKey>`
- `BaseIdentityRoleWithMetaSoftDeleteConcurrency<TKey>`

### `alaasmagi.Base.Contracts.DTO`

Defines:

- `IMapper<TUpperEntity, TLowerEntity, TKey>`
- `IMethodResponse<TValue>`
- `IError<TCode>`

### `alaasmagi.Base.DTO`

Provides:

- `MethodResponse<TValue>`
- `MethodResponse<TValue, TError>`
- `Error`
- `Error<TCode>`
- `ErrorDefaults`

### `alaasmagi.Base.Contracts.DataAccess`

Defines:

- `IBaseRepository<TEntity, TResourceKey, TActor>`
- `IBaseRepositorySoftDelete<TEntity, TResourceKey, TActor>`
- `IBaseUow` (`SaveChangesAsync`, and `TrySaveChangesAsync` which returns rejected writes as a failed `IMethodResponse<int>`)

### `alaasmagi.Base.DataAccess.EF`

Provides EF Core implementations such as:

- `BaseRepository<TDomainEntity, TDataAccessEntity, TMapper, TResourceKey, TActor>`
- `BaseRepositorySoftDelete<TDomainEntity, TDataAccessEntity, TMapper, TResourceKey, TActor>`
- `BaseUow<TDbContext>`

### `alaasmagi.Base.Contracts.Application`

Defines:

- `IBaseService<TEntity, TKey, TActor>`
- `IBaseServiceSoftDelete<TEntity, TKey, TActor>`
- `IBaseServiceUow`

### `alaasmagi.Base.Application`

Provides:

- `BaseService<TEntity, TDomainEntity, TRepository, TKey, TActor>`
- `BaseServiceSoftDelete<TEntity, TDomainEntity, TRepository, TKey, TActor>`
- `BaseServiceUow<TUow>`

### `alaasmagi.Base.Contracts.Exception`

Defines:

- `IBaseException<TCode>`
- `IHttpException<TCode>`

### `alaasmagi.Base.Exception`

Provides:

- `BaseException`
- `BaseException<TCode>`
- `HttpException`
- `HttpException<TCode>`

### `alaasmagi.Base.Contracts.Cache`

Defines:

- `IBaseCache`
- `IBaseCacheResult<TValue>`
- `IBaseCacheEntryOptions`
- `IBaseCacheSerializer`
- `IBaseCacheKeyBuilder`

### `alaasmagi.Base.Cache`

Provides:

- `BaseCache`
- `BaseCacheResult<TValue>`
- `BaseCacheEntryOptions`
- `BaseCacheOptions`
- `BaseCacheKeyBuilder`
- `BaseJsonCacheSerializer`

### `alaasmagi.Base.Contracts.Message`

Defines:

- `IBaseEventEnvelope<TContent>`
- `IBaseEventPublisher`
- `PublishResult`
- `IBaseEventHandler<TContent>`
- `IBaseEventDispatcher<TContent>`
- `MessageDelivery`, `MessageDisposition`

### `alaasmagi.Base.Contracts.Keycloak`

Defines the Keycloak service and data abstractions:

- `IKeycloakAdminClient`
- `IKeycloakUser`, `IKeycloakRole`

### `alaasmagi.Base.Message`

Provides:

- `BaseEventEnvelope<TContent>` (with `BaseEventEnvelope<TContent>.Create(...)` factory)
- `DefaultSources`
- `BaseRoutingKey`

### `alaasmagi.Base.Message.RabbitMQ`

Provides:

- `RabbitMqOptions`
- `RabbitMqConnectionManager`
- `RabbitMqEventPublisher`
- `RabbitMqConsumerBase<TContent>`
- `RabbitMqDeliveryConsumerBase` (raw delivery with routing key; the handler decides ack, requeue or reject)
- RabbitMQ publisher and consumer dependency injection helpers

### `alaasmagi.Base.Keycloak`

Provides:

- Keycloak JWT bearer authentication registration
- Keycloak OpenID Connect authentication registration (configurable scopes, cookie paths, claim types and PAR), and JWT bearer as an extra scheme next to it
- Keycloak role claim transformation
- Keycloak permission mapping (`KeycloakOptions.MapPermissions`) for JWT bearer and OpenID Connect: `resource_access.{own clientId}.roles` values containing a dot become `permission` claims; in OIDC mode they come from the stored access token and are refreshed with it
- contract token validation: JWKS signature, exact issuer, audience (`Audience`, falling back to `ClientId`), lifetime with `ClockSkew` (default 30 seconds)
- Keycloak admin API options and a typed `IKeycloakAdminClient` implementation
- `KeycloakUser` and `KeycloakRole` admin models
- identity event content payloads matching the message schema:
  - `UserEventContent` (`user-created`, `user-deleted`, `user-updated`, `user-enabled`, `user-disabled`)
  - `VerifyEmailContent` (`user-verify`)
  - `OtpEmailContent` (`user-2fa-otp`)
  - `PasswordResetContent` (`user-password-reset`)

### `alaasmagi.Base.Contracts.Authorization`

Defines:

- `ICurrentUser` (`IsAuthenticated`, `Id` from `sub`, `Email`, `Name`, `IsService`, `ClientId`, `Permissions`, `HasPermission`)

### `alaasmagi.Base.Authorization`

Provides (reads `permission` claims only; no Keycloak dependency):

- `Actions` (`View`, `ViewAll`, `Create`, `Edit`, `Delete`), extensible by apps
- `RequirePermissionAttribute` (`[RequirePermission("courses", Actions.Create)]` or `[RequirePermission("courses.create")]`)
- `PermissionPolicyProvider` for `perm:{module}.{action}` policies
- `RequirePermission(module, action)` for minimal API endpoints and groups
- `CurrentUser`, registered as scoped `ICurrentUser`
- `AddPermissionAuthorization(options => ...)` with a deny-by-default fallback policy and optional
  `ValidateEndpointsOnStartup`

## Typical Usage

A common setup looks like this:

1. Inherit from a `Base.Domain` entity type that matches your needs.
2. Implement an `IMapper` between your domain model and EF model if those types differ.
3. Inherit from `BaseRepository<...>` or `BaseRepositorySoftDelete<...>`.
4. If your application layer uses different models, implement another mapper between application and domain models.
5. Inherit from `BaseService<...>` or `BaseServiceSoftDelete<...>`.

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

## Framework and Dependencies

All packages in this repository currently target:

- `.NET 10.0`

All package projects currently declare:

- version `1.6.1`

Relevant package dependencies used across the set include:

- `Duende.AccessTokenManagement.OpenIdConnect`
- `Microsoft.AspNetCore.Authentication`
- `Microsoft.AspNetCore.Authentication.JwtBearer`
- `Microsoft.AspNetCore.Authentication.OpenIdConnect`
- `Microsoft.AspNetCore.Identity.EntityFrameworkCore`
- `Microsoft.EntityFrameworkCore`
- `Microsoft.Extensions.DependencyInjection`
- `Microsoft.Extensions.Hosting`
- `Microsoft.Extensions.Logging`
- `RabbitMQ.Client`

## Notes

- These packages are designed to be used together, but each package can also be consumed independently where appropriate.
- Keep package versions in sync across the `alaasmagi.Base.*` set.
- `Base.DataAccess.EF` is intended for EF Core based persistence.
- Repository and service methods return `IMethodResponse<T>` so calling code can handle success and failure consistently.
- Metadata timestamps and actor fields are populated automatically by the EF repository base classes when supported by the entity type.
- User-based scoping is applied automatically when the entity implements `IBaseEntityUserId<TActor>` and a non-default actor value is supplied.
- Cache packages provide string-keyed, typed value abstractions that can be backed by Redis or another key-value cache store.
- Message packages use the platform event envelope (`id`, `source`, `tenant`, `action`, `timestamp`, `contentVersion`, `content`); commands additionally include `expiresAt`. Routing segments must be non-empty and contain no dots.
- Keycloak helpers cover authentication registration, role claim transformation, admin user operations, and identity email/lifecycle event payloads.
- Authorization helpers check permissions only, never roles. Endpoints without `RequirePermission`, `[Authorize]`, or `[AllowAnonymous]` are denied.

## Developer

Developed and maintained by Aleksander Laasmägi.

- GitHub profile: [alaasmagi](https://github.com/alaasmagi)
- LinkedIn profile: [alaasmagi](https://www.linkedin.com/in/alaasmagi/)
- Email: [aleksander.laasmagi@gmail.com](mailto:aleksander.laasmagi@gmail.com)
- Repository: [base-nuget](https://github.com/alaasmagi/base-nuget)


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
