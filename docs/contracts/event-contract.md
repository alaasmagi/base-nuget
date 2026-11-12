# Event contract

Every message on the platform message bus (RabbitMQ) uses one envelope shape. This document is the source of truth for that shape, the routing key, and the messages published by the core hubs. `event-contract.json` contains the same examples in machine-readable form.

Implementation: `BaseEventEnvelope<TContent>` (`Base.Message`), `IBaseEventEnvelope<TContent>` (`Base.Contracts.Message`).

## Envelope

```json
{
  "id": "3f2c9a1e-…",
  "source": "identity-hub",
  "tenant": "educode",
  "action": "user-created",
  "timestamp": "2026-11-12T10:15:30.123Z",
  "expiresAt": "2026-11-12T10:25:30.123Z",
  "contentVersion": "1.0",
  "content": { }
}
```

| Field | Type | Required | Rules |
|---|---|---|---|
| `id` | string (UUID) | yes | New UUID per message. It is the idempotency key: consumers must handle the same `id` more than once safely. |
| `source` | string | yes | Publisher slug, kebab-case, no dots. Core hubs use `DefaultSources` (`identity-hub`, `notification-hub`, `message-hub`, `error-hub`, `ai-hub`). Products use their own app slug. |
| `tenant` | string | yes | The subject the message concerns: the Keycloak realm name. No dots. |
| `action` | string | yes | What happened or what to do. One kebab-case segment, no dots. |
| `timestamp` | string | yes | Publish time, UTC, ISO 8601, millisecond precision, `Z` suffix: `yyyy-MM-ddTHH:mm:ss.fffZ`. Never local time. |
| `expiresAt` | string \| null | commands only | See [Events and commands](#events-and-commands). UTC ISO 8601. |
| `contentVersion` | string | yes | Version of the `content` schema, e.g. `1.0`. Passed through verbatim. |
| `content` | object | yes | Payload. Its schema is defined by `action` + `contentVersion`. |

## Events and commands

The presence of the `expiresAt` field is what distinguishes the two:

- **Event** - something that happened (`user-created`). The `expiresAt` field is **absent**. Any number of consumers may react.
- **Command** - a request to do something (`user-verify`: send a verification email). The `expiresAt` field is **present**:
  - a timestamp: the command must not be executed after that moment;
  - `null`: the command never expires.

Create events with `BaseEventEnvelope<T>.Create(...)` and commands with `BaseEventEnvelope<T>.CreateCommand(...)`.

Consumers must check `expiresAt` themselves before executing a command. The consumer base classes do not drop expired commands.

## Routing key

```
{source}.{tenant}.{action}
```

Example: `identity-hub.educode.user-2fa-otp`. Build it only with `BaseRoutingKey.For(envelope)`, never by hand. A dot in any segment is rejected, because it would shift the segments and silently break consumer bindings.

Exchange and queue topology is set up manually, not in code.

## Delivery

- `RabbitMqConsumerBase<T>`: acks when the handler returns. If deserialization or the handler throws, the message is rejected without requeue (dead-lettered if a dead-letter binding exists).
- `RabbitMqDeliveryConsumerBase<T>`: the handler returns a `MessageDisposition` (`Ack`, `Requeue`, `Reject`) for per-message control.

## Versioning

- Adding an optional field to `content` does not change `contentVersion`.
- Removing or renaming a field, changing a type or making a field required means a new `contentVersion` (e.g. `2.0`). Publishers keep sending the old version until all consumers handle the new one.
- The envelope fields themselves do not change.

## Common content fields

| Field | Type | Rules |
|---|---|---|
| `userId` | string (UUID) | The Keycloak user id, the same value as the JWT `sub`. The only user identifier. |
| `email` | string | Display and delivery only, never an identifier. |
| `fullName` | string | Display only. |
| `username` | string | Display only. |
| `locale` | string | BCP 47 with region, e.g. `et-EE`. |
| `timeZone` | string | IANA identifier, e.g. `Europe/Tallinn`. |

**Notification commands** (anything addressed to one person, content based on `BaseNotificationContent`) must include `userId`, `email`, `fullName`, `locale` and `timeZone`. `BaseNotificationContent.Validate()` enforces this before publishing.

## Identity events

Published by `identity-hub` (Keycloak event listener). All are events (no `expiresAt`).

| Action | When | Content |
|---|---|---|
| `user-created` | User registered or created | `userId`, `email`, `username`, `fullName`, `locale` |
| `user-updated` | Profile changed | `userId`, `email`, `username`, `fullName`, `locale` |
| `user-enabled` | User enabled | `userId`, `email`, `username`, `fullName`, `locale` |
| `user-disabled` | User disabled | `userId`, `email`, `username`, `fullName`, `locale` |
| `user-deleted` | User deleted | `userId` only |

```json
{
  "id": "3f2c9a1e-…",
  "source": "identity-hub",
  "tenant": "educode",
  "action": "user-created",
  "timestamp": "2026-11-12T10:15:30.123Z",
  "contentVersion": "1.0",
  "content": {
    "userId": "8d1b…",
    "email": "user@example.com",
    "username": "user",
    "fullName": "Some User",
    "locale": "et-EE"
  }
}
```

## Identity commands

Published by `identity-hub`, consumed by `notification-hub`. All are commands with an expiry. Content = common notification fields plus:

| Action | Extra content | Typical validity |
|---|---|---|
| `user-verify` | `actionLink`, `expiresAt`, `validForMinutes` | 10 min |
| `user-2fa-otp` | `otpCode`, `expiresAt`, `validForMinutes` | 5 min |
| `user-password-reset` | `actionLink`, `expiresAt`, `validForMinutes` | 10 min |

`content.expiresAt` is shown to the user. The envelope `expiresAt` is authoritative for whether the command may still be executed; both carry the same instant.

```json
{
  "id": "3f2c9a1e-…",
  "source": "identity-hub",
  "tenant": "educode",
  "action": "user-2fa-otp",
  "timestamp": "2026-11-12T10:15:30.123Z",
  "expiresAt": "2026-11-12T10:20:30.123Z",
  "contentVersion": "1.0",
  "content": {
    "userId": "8d1b…",
    "email": "user@example.com",
    "fullName": "Some User",
    "locale": "et-EE",
    "timeZone": "Europe/Tallinn",
    "otpCode": "123456",
    "expiresAt": "2026-11-12T10:20:30.123Z",
    "validForMinutes": 5
  }
}
```

## Product notification commands

Published by products (`source` = app slug), consumed by `notification-hub`. Content = common notification fields plus the action-specific fields below. Amounts are decimal strings (`"95.75"`), dates are `yyyy-MM-dd`, times are `HH:mm`.

### `invoice` (never expires: `expiresAt: null`)

| Field | Type |
|---|---|
| `invoiceIdentifier` | string |
| `invoicePeriod` | string, `yyyy-MM` |
| `invoiceDate`, `invoiceDueDate` | date |
| `invoiceTotal` | decimal string |
| `currency` | ISO 4217, e.g. `EUR` |
| `invoiceRows[]` | `address`, `service`, `periodStart`, `periodEnd`, `invoiceAmount`, `fractions` (int), `invoiceFraction` |
| `paymentFullName`, `paymentIban`, `paymentDescription` | string |

### `lunch-recommendation` (expires)

| Field | Type |
|---|---|
| `currency` | ISO 4217 |
| `recommendationRows[]` | `restaurantName`, `offers[]` (`offerText`, `offerPrice`), `offerTimeFrom`, `offerTimeUntil`, `link` |
| `linkToUserWheel` | string (URL) |

### Adding a new notification command

1. Pick an `action` (kebab-case, one segment) and start at `contentVersion` `1.0`.
2. Content inherits `BaseNotificationContent` (implement `IExpiringNotificationContent` if it expires).
3. Publish with `CreateCommand(...)`.
4. Add the action and its content fields to this document and to `event-contract.json`.