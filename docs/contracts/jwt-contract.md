# JWT contract

Every backend on the platform authenticates with Keycloak access tokens. This document is the source of truth for which claims a backend may rely on, how tokens are validated, and how permissions are carried.

Implementation: `Base.Keycloak` (token validation, permission mapping), `Base.Authorization` (permission checks), `ICurrentUser` (`Base.Contracts.Authorization`).

## Access token

```json
{
  "iss": "https://identity.alaasmagi.dev/realms/educode",
  "sub": "3f2c9a1e-…",
  "aud": ["educode-api"],
  "azp": "educode-web",
  "exp": 1794382100,
  "iat": 1794381800,
  "jti": "…",

  "email": "user@example.com",
  "email_verified": true,
  "name": "Some User",

  "resource_access": {
    "educode-api": { "roles": ["course-management", "courses.create", "courses.edit"] }
  }
}
```

## Claims

| Claim | Meaning | Rules |
|---|---|---|
| `iss` | Issuer | Must exactly match the backend's realm URL `https://identity.alaasmagi.dev/realms/{realm}`. |
| `sub` | User id (UUID) | The only user identifier. Domain data references users by this value. Same value as `userId` in the event contract. |
| `aud` | Target backend | Must contain the backend's own client id, otherwise the token is rejected. |
| `azp` | Calling client | Logging and auditing only. Never used for authorization. |
| `exp`, `iat`, `jti` | Lifetime, issue time, token id | Standard JWT handling. |
| `email` | Email | Display and delivery only. Never an identifier: it can change. |
| `email_verified` | Email verified | Informational. |
| `name` | Full name | Display only. |
| `resource_access.{clientId}.roles` | Permissions | The only source of authorization. `{clientId}` is the backend's own client id. See [Permissions](#permissions). |
| `client_id` | Service account client id | Present only in service tokens. See [Service tokens](#service-tokens). |

Claims not listed here are ignored, in particular:

- `realm_access` - realm roles, including Keycloak defaults (`offline_access`, `uma_authorization`, ...).
- `preferred_username` - can change, not an identifier.
- `resource_access` entries of **other** clients.

Backends read claims with their raw names (`MapInboundClaims = false`).

## Permissions

- A **permission** is a Keycloak client role on the backend's client, named `{module}.{action}` with exactly one dot, e.g. `courses.create`. Module and action are kebab-case.
- A **role** is a Keycloak composite client role made of permissions, e.g. `course-management` = `courses.create` + `courses.edit`. Role names never contain a dot.
- Users and service accounts are assigned roles, not individual permissions. Changing what a role grants is a Keycloak change, not a code change.
- Keycloak puts all effective roles into the token, including the composite role name itself. Mapping rule:
  - value **with** a dot = permission, mapped to a `permission` claim;
  - value **without** a dot = role name, ignored.
- Code checks permissions only (`[RequirePermission]`, `ICurrentUser.HasPermission`). It never checks role names.
- Standard actions: `view`, `view-all`, `create`, `edit`, `delete` (`Actions` in `Base.Authorization`). Apps add their own actions by extending `Actions`.
- Every permission used in code must exist as a client role in Keycloak.

## Validation

Every backend validates:

1. Signature against the realm JWKS (from the discovery document).
2. `iss` - exact match.
3. `aud` - contains the backend's own client id.
4. `exp` (and `nbf` when present) - with at most **30 seconds** clock skew. Servers keep their clocks synced (NTP).

Responses:

- Missing, invalid or expired token: **401**.
- Valid token without the required permission: **403**.

## Lifetimes

- Access token: **5 minutes**. This is also the maximum delay before a role or permission change takes effect.
- Refresh token: per app.
- There is no immediate revocation. Disabling a user or removing a role takes effect when the current access token expires.

## Service tokens

Synchronous backend-to-backend HTTP calls use the client credentials flow: the calling backend's client has a service account. Asynchronous work goes over the message bus (see the event contract) and needs no token.

```json
{
  "iss": "https://identity.alaasmagi.dev/realms/educode",
  "sub": "9a7e…",
  "aud": ["target-api"],
  "azp": "educode-api",
  "client_id": "educode-api",
  "exp": 1794382100,
  "iat": 1794381800,
  "jti": "…",

  "resource_access": {
    "target-api": { "roles": ["reports.create"] }
  }
}
```

- `sub` is the service account's user id, not a person. Domain data never references it as a user.
- `client_id` is present and `email` is absent. This is how a backend tells a service from a user (`ICurrentUser.IsService`).
- Permissions work the same way: the service account is assigned roles of the target backend's client.
- Both backends are clients in the **same realm**. Cross-realm calls are not supported.

## Clients

| Client type | Authentication | Where permissions come from |
|---|---|---|
| API backend | JWT bearer | `resource_access` in the bearer token. |
| MVC (Razor Pages) | Keycloak OIDC + cookie | `resource_access` in the **access token** stored in the cookie, refreshed with it. The ID token is never used for authorization. |
| SPA / mobile | Authorization code + PKCE (public client) | Calls the API with its access token; the API applies the rules above. |

## `ICurrentUser`

| Member | Source |
|---|---|
| `Id` | `sub`. Throws when there is no valid `sub`. |
| `IsAuthenticated` | Principal is authenticated and has a valid `sub`. Check it before reading `Id` on anonymous endpoints. |
| `Email` | `email` |
| `Name` | `name` |
| `IsService` | `client_id` present and `email` absent |
| `ClientId` | `client_id` (service tokens only) |
| `Permissions` / `HasPermission(module, action)` | `permission` claims mapped from `resource_access.{own clientId}.roles` |

## Keycloak configuration

Required for the contract to hold:

1. Each backend is its own client. Its permissions are client roles; its roles are composite client roles.
2. Frontend and mobile clients have **Full scope allowed** turned off and get only the client roles of the backend they call (client scope with role scope mappings).
3. An **audience mapper** puts the backend's client id into `aud` for tokens issued to its frontend/mobile clients. Without it, a user with no roles on the backend gets a token without that `aud` and receives 401 instead of 403.
4. Service accounts are assigned roles of the backends they call.
5. Default roles (Realm settings → User registration) contain only roles every new user should have. Roles of private apps are never default roles.