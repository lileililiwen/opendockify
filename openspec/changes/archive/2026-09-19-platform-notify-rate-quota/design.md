## Context

Custom `IdempotencyRecords`, `OutboxEvents`, per-IP limiters, HMAC webhooks. Platform contracts (`IIdempotencyStore`, `IRateLimiter`, `IQuotaStore`, durable outbox, webhook contracts) are behavior-compatible and tested.

## Goals / Non-Goals

Goals: SMTP notifications, per-user limits/quotas, platform idempotency/outbox/webhooks, link passwords.
Non-goals: SendGrid/SMS/RabbitMQ/billing (see proposal).

## Decisions

- **Mail**: `SmtpMailService` (MailKit), `Mailing:Smtp:*` validated; `Notifications:Enabled=false` default; dev fallback logs to server log (no PII beyond to/subject).
- **Intents**: share-granted, link-expiring-24h, document-finalized; templates plaintext (no Razor in this change).
- **Limits**: `IRateLimiter` per `policy|user-or-ip`; policies `generate/preview/finalize/ai-polish` added to catalog; `Quota.AspNetCore` 429 RFC9457 with `Retry-After`.
- **Idempotency/outbox**: `IIdempotencyStore` (EF adapter, 24h retention) keyed by `Idempotency-Key`; `Eventing.EfCore` outbox replaces `OutboxEvents`; webhook HMAC `t,v1` preserved, payload versioned `v1`.
- **Link passwords**: Argon2/PBKDF2 hash, `POST /s/{token}` with password field, constant-time verify, 5-fail lockout per link.

## Risks / Trade-offs

- [Risk: SMTP misconfig spams/fails] -> Mitigation: disabled by default, health status, transient/permanent classification, no secrets in logs.
- [Risk: stricter limits break automation] -> Mitigation: per-token buckets generous (60/min preserved), docs, `Retry-After` handling in Flutter/sample.
- Licensing: none.

## Migration Plan

1. Add refs; implement mail/notifications (disabled default).
2. Swap limiters/quota/idempotency/outbox/webhooks; add link-password.
3. Require `Sharing:HashKey`; smoke mailhog + limits + replay + webhook retry.
