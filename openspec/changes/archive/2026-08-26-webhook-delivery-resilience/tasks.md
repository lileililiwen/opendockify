## 1. Tests First

- [x] 1.1 Rotation tests: owner-scoped, secret changes, URL/event types preserved, old signature rejected
- [x] 1.2 Transient-failure test: DNS failure keeps delivery pending with backoff; policy violation still blocks
- [x] 1.3 Attempt-timeline test: attempts recorded (max 5) and exposed in views without bodies/secrets

## 2. Resilience Implementation

- [x] 2.1 Add `AttemptLog` column + configuration with migration; append entries in `DeliverAsync`
- [x] 2.2 Classify plan failures: transient set stays pending, policy violations block
- [x] 2.3 Add `RotateSecretAsync` and the management rotate endpoint returning the secret once
- [x] 2.4 Expose typed attempt timeline in `WebhookDeliveryView`

## 3. Verify and Deliver

- [x] 3.1 Build zero-warning, run unit/architecture gates, update docs (rotation + transient behavior)
- [x] 3.2 Archive and commit only related paths
