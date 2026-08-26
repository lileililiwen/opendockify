## 1. Tests First

- [x] 1.1 DTO parsing tests for token/subscription/delivery payloads (incl. attempts timeline)
- [x] 1.2 Widget tests: one-time clear-token reveal + copy, revoke confirmation, rotation reveal, retry refresh, redaction of secrets in lists

## 2. App Implementation

- [x] 2.1 Add `integrations.dart` DTOs and ApiClient methods with ApiError mapping
- [x] 2.2 Add Riverpod providers/controllers for tokens, subscriptions, deliveries
- [x] 2.3 Build the Integrations screen (Tokens / Webhooks / Deliveries) with create sheets, one-time secret dialogs, confirmations
- [x] 2.4 Register `/integrations` route and Settings entry point

## 3. Verify and Deliver

- [x] 3.1 `flutter analyze` clean and `flutter test` green; exercise flows against a running API
- [x] 3.2 Archive and commit only related paths
