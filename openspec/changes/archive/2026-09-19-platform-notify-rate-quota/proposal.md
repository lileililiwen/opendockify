## Why

Notifications are zero (no email on share/expire/finalize); rate limiting is IP-only with no user key and no coverage on generate/preview/AI; quota has no abstraction; idempotency/outbox/webhooks are bespoke. Platform libs provide all four as tested contracts.

## What Changes

- Adopt `Platform.Mailing` + `Platform.Mailing.Smtp` (self-hosted SMTP default; SendGrid explicitly NOT adopted per self-hosted constraint) + `Platform.Notifications` (share-granted, link-expiring, document-finalized intents via `IMailService`, console-dev fallback).
- Adopt `Platform.RateLimiting` (per-user + per-IP keys, policies for `generate/preview/finalize/ai-polish`) + `Platform.Quota.AspNetCore` (per-user daily AI/document quotas, 429 RFC9457) + `Platform.Idempotency` (`IIdempotencyStore` replaces bespoke automation replay) + `Platform.Eventing.EfCore` (durable outbox replaces custom `OutboxEvents`) + `Platform.Webhooks.*` (HMAC, SSRF guard, retry via Jobs).
- Add link-password option for public shares (PBKDF2-hashed, constant-time verify).
- Separate `Sharing:HashKey` required (no JWT-secret fallback).

## Capabilities

### New Capabilities
- `notify-rate-quota`: SMTP notifications, per-user rate/quota enforcement, platform idempotency/outbox/webhooks, link passwords.

### Modified Capabilities
None — existing share/automation routes preserved; new 429/quotas additive, new mail intents opt-in via `Notifications:Enabled=false` default.

## Non-goals

- No SendGrid/Mailgun/SMS/push in this change (self-hosted SMTP only; SMS is follow-up).
- No Stripe/LemonSqueezy billing (out of scope; self-hosted only).
- No RabbitMQ transport (EF outbox + in-process dispatch only).
- No breaking webhook payload changes (versioned instead).

## Impact

- New refs: `Platform.Mailing`, `Platform.Mailing.Smtp`, `Platform.Notifications`, `Platform.RateLimiting`, `Platform.Quota`, `Platform.Quota.AspNetCore`, `Platform.Idempotency`, `Platform.Eventing.Contracts`, `Platform.Eventing.EfCore`, `Platform.Webhooks.Contracts`, `Platform.Webhooks.AspNetCore`, `Platform.Webhooks.EfCore`.
- `Sharing`/`Integrations`/`Generation`/`AiAssist`: swap to platform stores; `Api`: new mail/audit endpoints; config `Mailing:Smtp:*`, `Notifications:Enabled`, `RateLimiting:*`, `Quota:*`.
- Docker: no new service; deployer supplies SMTP host.
- Flutter: notification preferences screen (enabled/disabled), link-password field.
