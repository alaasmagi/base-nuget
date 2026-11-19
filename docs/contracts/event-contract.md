# Event contract

Every message on the platform message bus (RabbitMQ) uses one envelope shape. This document is the source of truth for that shape, the routing key, and the messages published by the core hubs.

Implementation: `BaseEventEnvelope<TContent>` (`Base.Message`), `IBaseEventEnvelope<TContent>` (`Base.Contracts.Message`).

## Envelope

Every message has exactly these fields. Consumers must not depend on the order of fields.

```json
{
  "id": "3f2c9a1e-…",
  "source": "identity-hub",
  "tenant": "educode",
  "action": "email-user-2fa-otp",
  "timestamp": "2026-11-12T10:15:30.123Z",
  "contentVersion": "1.0",
  "content": { }
}
```

| Field | Type | Rules |
|---|---|---|
| `id` | string (UUID) | New UUID per message. It is the idempotency key: consumers must handle the same `id` more than once safely, see [Idempotency](#idempotency). |
| `source` | string | Publisher slug, kebab-case, no dots. Core hubs use `DefaultSources` (`identity-hub`, `notification-hub`, `message-hub`, `error-hub`, `ai-hub`). Products use their own app slug. |
| `tenant` | string | The subject the message concerns: the Keycloak realm name, or the requesting product's slug for ai-hub replies. No dots. |
| `action` | string | What happened or what to do. One kebab-case segment, no dots. |
| `timestamp` | string | Publish time, UTC, ISO 8601, millisecond precision, `Z` suffix: `yyyy-MM-ddTHH:mm:ss.fffZ`. Never local time. |
| `contentVersion` | string | Version of the `content` schema, e.g. `1.0`. Passed through verbatim. |
| `content` | object | Payload. Its schema is defined by `action` + `contentVersion`. Never `null`; use `{}` if there is nothing to send. |

No envelope field is ever `null`. Consumers reject a message that is missing any envelope field. Anything specific to one action belongs in `content`.

## Events and commands

The envelope shape is the same for both. Whether a message is an event or a command is decided by the exchange it is published on, not by the envelope.

- **Event** - something that happened (`user-created`). Published on the publisher's own `{source}.events` exchange. Any number of consumers may react.
- **Command** - a request to do something (`email-user-verify`: send a verification email). Published on the executing hub's `{hub}.commands` exchange.

## Exchanges

All are durable `topic` exchanges, bound by routing key.

| Exchange | Kind | Published by | Consumed by |
|---|---|---|---|
| `identity-hub.events` | events | `identity-hub` | any |
| `notification-hub.commands` | commands | `identity-hub`, products | `notification-hub` |
| `ai-hub.commands` | commands | products | `ai-hub` |
| `ai-hub.events` | events | `ai-hub` | products (`ai-hub.{product}.#`) |

## Routing key

```
{source}.{tenant}.{action}
```

Example: `identity-hub.educode.email-user-2fa-otp`. Build it only with `BaseRoutingKey.For(envelope)`, never by hand. A dot in any segment is rejected, because it would shift the segments and silently break consumer bindings.

Exchange and queue topology is set up manually, not in code. A new hub that publishes events gets `{hub}.events`; a hub that executes commands gets `{hub}.commands`.

## Delivery

Delivery is at-least-once. A message can arrive more than once, and a consumer can crash after doing the work but before acking.

### Queues and dead-lettering

Every consumer queue is set up like this (manually, like the rest of the topology):

| Setting | Value |
|---|---|
| Type | quorum (`x-queue-type: quorum`) |
| `x-delivery-limit` | `5` |
| `x-dead-letter-exchange` | `dead-letter` |
| `x-dead-letter-routing-key` | `{app}` |

`dead-letter` is one durable `direct` exchange. Every app has a durable quorum queue `{app}.dlq` bound to it with routing key `{app}`. Dead-lettering is mandatory: a consumer queue without these settings is a configuration error.

A message ends up in `{app}.dlq` when it is rejected, or when it has been redelivered more than `x-delivery-limit` times. The original exchange and routing key are kept in the `x-death` header.

### Consumer base classes

- `RabbitMqConsumerBase<T>`: acks when the handler returns. If deserialization or the handler throws, the message is rejected without requeue, i.e. dead-lettered.
- `RabbitMqDeliveryConsumerBase<T>`: the handler returns a `MessageDisposition` (`Ack`, `Requeue`, `Reject`) for per-message control.

Return `Requeue` only for transient failures: a dependency that is temporarily unavailable (SMTP server, database, downstream HTTP service). Everything else (invalid content, unknown `contentVersion`, a failure that will happen again on retry) is `Reject`. `x-delivery-limit` is the safety net that stops a message from looping forever.

RabbitMQ redelivers a requeued message immediately, which would use up `x-delivery-limit` in milliseconds. `RabbitMqDeliveryConsumerBase<T>` therefore waits before nacking with requeue (`Task.Delay`, default 5 s, configurable). This holds up the consumer's prefetch for that time, which is acceptable at the platform's volume.

An outage longer than the retries cover (5 deliveries × delay) still sends messages to `{app}.dlq`. Messages there are inspected and moved back to the original exchange manually (shovel), once the cause is fixed. Nothing is replayed from a DLQ automatically.

### Idempotency

Every consumer keeps the `id`s it has processed in its own database:

```sql
CREATE TABLE processed_messages (
  message_id   uuid        PRIMARY KEY,
  processed_at timestamptz NOT NULL,
  status       text        NULL      -- only used by consumers with external side effects
);
```

- If the work only changes the consumer's own database, insert the `id` in the same transaction. A unique violation means the message is a duplicate: ack it and stop.
- If the work has an external side effect that cannot be in the transaction (`notification-hub` sending an email):
    1. Insert the `id` with status `sending`. On conflict, read the existing row: if the status is `sent`, ack and stop; if it is `sending`, continue (the earlier attempt crashed or was requeued before finishing).
    2. Do the work (send the email).
    3. Set status `sent`, then ack.

  No email is lost. A crash between steps 2 and 3 causes one duplicate email; that is accepted.
- Rows are deleted after 7 days.

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

Published by `identity-hub` (Keycloak event listener) on `identity-hub.events`. All are events.

`locale` and `timeZone` are always filled, see [Locale and time zone](#locale-and-time-zone).

| Action | When | Content |
|---|---|---|
| `user-created` | User registered or created | `userId`, `email`, `username`, `fullName`, `locale`, `timeZone` |
| `user-updated` | Profile changed | `userId`, `email`, `username`, `fullName`, `locale`, `timeZone` |
| `user-enabled` | User enabled | `userId`, `email`, `username`, `fullName`, `locale`, `timeZone` |
| `user-disabled` | User disabled | `userId`, `email`, `username`, `fullName`, `locale`, `timeZone` |
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
    "locale": "et-EE",
    "timeZone": "Europe/Tallinn"
  }
}
```

### Locale and time zone

The Keycloak extension fills `locale` and `timeZone` for every identity event and command, so `BaseNotificationContent.Validate()` never fails on them.

| Field | Source | If missing or invalid |
|---|---|---|
| `timeZone` | User attribute `timeZone` | `Europe/Tallinn` |
| `locale` | Built-in user attribute `locale` | The realm's default locale, otherwise `et-EE` |

Invalid means not a valid IANA time zone identifier (`timeZone`) or not a BCP 47 tag with region (`locale`). The extension validates the value and uses the fallback instead of publishing it.

## Identity commands

Published by `identity-hub` on `notification-hub.commands`, consumed by `notification-hub`. All are commands. Content = common notification fields plus:

| Action | Extra content | Typical validity |
|---|---|---|
| `email-user-verify` | `actionLink`, `expiresAt`, `validForMinutes` | 10 min |
| `email-user-2fa-otp` | `otpCode`, `expiresAt`, `validForMinutes` | 5 min |
| `email-user-password-reset` | `actionLink`, `expiresAt`, `validForMinutes` | 10 min |

`content.expiresAt` and `validForMinutes` are display data: the email shows the user how long the code or link is valid.

```json
{
  "id": "3f2c9a1e-…",
  "source": "identity-hub",
  "tenant": "educode",
  "action": "email-user-2fa-otp",
  "timestamp": "2026-11-12T10:15:30.123Z",
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

Published by products (`source` = app slug) on `notification-hub.commands`, consumed by `notification-hub`. All are commands. Content = common notification fields plus the action-specific fields below. Amounts are decimal strings (`"95.75"`), dates are `yyyy-MM-dd`, times are `HH:mm`.

### `email-invoice` (command)

| Field | Type |
|---|---|
| `invoiceIdentifier` | string |
| `invoicePeriod` | string, `yyyy-MM` |
| `invoiceDate`, `invoiceDueDate` | date |
| `invoiceTotal` | decimal string |
| `currency` | ISO 4217, e.g. `EUR` |
| `invoiceRows[]` | `address`, `service`, `periodStart`, `periodEnd`, `invoiceAmount`, `fractions` (int), `invoiceFraction` |
| `paymentFullName`, `paymentIban`, `paymentDescription` | string |

### `email-lunch-recommendation` (command)

| Field | Type |
|---|---|
| `currency` | ISO 4217 |
| `recommendationRows[]` | `restaurantName`, `offers[]` (`offerText`, `offerPrice`), `offerTimeFrom`, `offerTimeUntil`, `link` |
| `linkToUserWheel` | string (URL) |

### Adding a new notification command

1. Pick an `action` (kebab-case, one segment) and start at `contentVersion` `1.0`.
2. Content inherits `BaseNotificationContent` (implement `IExpiringNotificationContent` if the email shows a validity time).
3. Publish on `notification-hub.commands`.
4. Add the action and its content fields to this document.

## AI parsing (ai-hub)

Products send work to `ai-hub` as commands on `ai-hub.commands` and receive the results as events on `ai-hub.events`. The reply's `tenant` is the requesting product's slug, so a product binds its own queue to `ai-hub.events` with `ai-hub.{product}.#`. Content is not a notification: it does not inherit `BaseNotificationContent` and carries no recipient fields.

### `invoice-parse` (command)

Published by `invoice-service` (`invoice-service.invoice-service.invoice-parse` on `ai-hub.commands`).

| Field | Type | Rules |
|---|---|---|
| `documentId` | string (UUID) | Correlation id; returned unchanged in the reply. |
| `fileUrl` | string (URL) | Signed GET link to the PDF. Secret: never log or persist it. |
| `fileName` | string | Original file name, display only. |
| `contentType` | string | `application/pdf`. |
| `sha256` | string | Lower-case hex SHA-256 of the file, to verify the download. |

### `invoice-parse-success` (event)

Published by `ai-hub` (`ai-hub.invoice-service.invoice-parse-success` on `ai-hub.events`).

| Field | Type | Rules |
|---|---|---|
| `documentId` | string (UUID) | From the command. |
| `serviceName` | string | Provider or service name as printed, e.g. `Telia`. |
| `invoiceDate` | date \| null | `yyyy-MM-dd`. |
| `periodStart`, `periodEnd` | date \| null | `yyyy-MM-dd`. |
| `currency` | string | ISO 4217, e.g. `EUR`. |
| `rows[]` | array | One per address charge: `addressText` (string), `amount` (decimal string), optional `invoiceDate`, `periodStart`, `periodEnd` (date \| null) that override the document-level values. |

### `invoice-parse-failure` (event)

Published by `ai-hub` (`ai-hub.invoice-service.invoice-parse-failure` on `ai-hub.events`).

| Field | Type | Rules |
|---|---|---|
| `documentId` | string (UUID) | From the command. |
| `reason` | string | One of `download-failed`, `not-an-invoice`, `unreadable`, `model-error`. See below. |
| `message` | string | Short explanation for the user. Never the document's content. |

| Reason | Final | What `invoice-service` does |
|---|---|---|
| `not-an-invoice` | yes | Shows the error to the user. Does not retry. |
| `unreadable` | yes | Shows the error to the user. Does not retry. |
| `download-failed` | no | May retry: a new `invoice-parse` command with the same `documentId` and a new signed `fileUrl`. An expired `fileUrl` also ends up here. |
| `model-error` | no | May retry, the same way. |

Because a retry reuses the `documentId`, the reply to an earlier attempt can arrive after the reply to a later one. `invoice-service` uses the first final result per `documentId` (`invoice-parse-success`, or a failure with a final reason) and ignores any later reply for that `documentId`.

```json
{
  "id": "6a0e…",
  "source": "ai-hub",
  "tenant": "invoice-service",
  "action": "invoice-parse-success",
  "timestamp": "2026-11-18T10:15:30.123Z",
  "contentVersion": "1.0",
  "content": {
    "documentId": "c4f1…",
    "serviceName": "Telia",
    "invoiceDate": "2026-11-05",
    "periodStart": "2026-10-01",
    "periodEnd": "2026-10-31",
    "currency": "EUR",
    "rows": [
      { "addressText": "Tallinn - Tehnika", "amount": "40.00" }
    ]
  }
}
```