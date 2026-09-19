## 1. Mail and notifications

- [x] 1.1 Add pinned refs `Platform.Mailing`, `Platform.Mailing.Smtp`, `Platform.Notifications`; wire `SmtpMailService` disabled-by-default
- [x] 1.2 Implement share/expiry/finalize intents (plaintext); dev fallback log; Flutter prefs + link-password field

## 2. Limits and webhooks

- [x] 2.1 Swap to `IRateLimiter` per-user policies + `Quota.AspNetCore` 429 RFC9457; add AI/document quotas
- [x] 2.2 Swap to `IIdempotencyStore` + `Eventing.EfCore` outbox + `Webhooks.*` HMAC/retry via Jobs; require `Sharing:HashKey`; add link-password hash/verify/lockout

## 3. Verify

- [x] 3.1 `dotnet build` 0/0; mailhog share/expiry/finalize, per-user 429 isolation, idempotent replay single-doc
- [x] 3.2 Webhook HMAC retry, link-password grant/401/lockout; `openspec validate --change platform-notify-rate-quota --strict`
