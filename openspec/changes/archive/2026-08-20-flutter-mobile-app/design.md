## Context

The backend exposes a JSON REST API (Minimal APIs, JWT auth) with a stable,
well-documented contract exercised by the existing HTTP smoke tests:

- `POST /api/auth/register|login`, `GET /api/auth/me`
- `GET /api/templates/marketplace`, `GET /api/templates/{id}`, template CRUD +
  `POST /{id}/copy`, `POST /api/admin/templates`
- `GET /api/documents` (paged), `POST /api/documents/generate`,
  `POST /{id}/reedit`, `GET /{id}`, `GET /{id}/download` (PDF),
  `DELETE /{id}`
- `POST /api/ai/polish-clause|polish-document`
- `GET /api/admin/settings`, `PUT /api/admin/settings/{key}`,
  `GET /api/admin/ai-usage`
- `GET /healthz`

Template shapes are JSON-serialized DTOs; `DefinitionJson` is a
`TemplateDefinition` (`fields[]` with `type` in Text/Number/Currency/Date,
`required`, optional `validation`, and `clauses[]` with `id/title/text`).
Documents return `snapshotJson` (filled values + selected clause ids),
`renderedText`, and a `downloadUrl`. Errors are `{ "error": "..." }` with the
matching HTTP status.

The app must remain self-hosted-first: the server base URL is user-configured
at runtime (and persisted), never hardcoded or pinned to a SaaS endpoint.

## Goals / Non-Goals

**Goals:**
- A production-quality Flutter app (Android/iOS) covering auth, templates,
  document generation/export, AI polish, and admin screens.
- Every API interaction goes through one typed client with a single JWT
  injection point and centralized error handling.
- Mandatory legal disclaimer visible in the app, plus the template risk notice
  on generated documents.
- High-quality tests: unit tests for parsing/validation, widget tests for each
  feature, and a contract test that runs against a live local API.

**Non-Goals:**
- Backend changes of any kind.
- Offline editing, push notifications, telemetry, third-party crash reporting.
- Refresh-token rotation (re-login on 401 is acceptable for the MVP).

## Decisions

- **Repository**: separate `opendockify-app` repo, mirroring the existing
  frontend/backend decoupling (README already declares "React SPA (separate
  repository)").
- **State management**: `flutter_riverpod` (compile-safe providers, easy
  testing) with `riverpod_generator` for providers and a single
  `ApiClientProvider` for the typed client.
- **HTTP client**: `dio` with one `AuthInterceptor` (injects the stored JWT)
  and one `ErrorInterceptor` (normalizes Dio exceptions into typed app errors;
  emits a 401 event to the auth layer). The `downloadUrl` returned by the API
  is absolute-ized against the configured base URL.
- **Token storage**: `flutter_secure_storage` (Keychain/Keystore). Base URL and
  non-secret preferences use `shared_preferences`. Tokens are never logged or
  written to plaintext storage.
- **Routing**: `go_router` with route guards — unauthenticated → login; admin
  routes require `isAdministrator` from `/me`.
- **Form rendering**: a widget (`DynamicTemplateForm`) that consumes the parsed
  `TemplateDefinition` and renders `TextField`/`NumberField`/`CurrencyField`/
  `DatePickerField` with required-markers and client-side validation mirroring
  the backend rules (`min/max`, `minLength/maxLength`, `pattern`,
  `dateFrom/dateTo`), plus clause toggles (switch/chip per clause). Currency
  fields capture a numeric amount; the RMB-uppercase rendering is left to the
  backend-rendered document.
- **PDF export**: download the PDF bytes from `downloadUrl`, save to the
  app documents directory, and open via `share_plus`/`open_filex` (platform
  viewer). No in-app PDF rendering library is required for the MVP.
- **Server configuration**: a "Connection" screen (shown on first launch and
  reachable from settings) where the user enters the base URL; the app pings
  `/healthz` and validates before saving. Cleared on "reset".
- **Environment/secrets**: base URL and JWT are runtime/persisted values, not
  compile-time constants. No API keys are embedded in the app.
- **Android cleartext**: HTTP (non-TLS) is allowed only for localhost/LAN
  development via a debug-only `usesCleartextTraffic` or network-security
  config; release builds MUST require HTTPS unless the deployer opts in.
- **Naming**: package `org.opendockify.app`, display name "OpenDockify".
- **Licensing**: MIT, matching the backend; no AGPL dependencies are pulled in.

## Risks / Trade-offs

- [Risk: server base URL misconfiguration] → Mitigation: connection screen with
  `/healthz` probe, stored per device, easily resettable; clear error strings.
- [Risk: JWT storage on device] → Mitigation: `flutter_secure_storage`; tokens
  held in memory only after load; wiped on logout.
- [Risk: API contract drift] → Mitigation: contract test suite runs against a
  local API instance in CI; DTOs are hand-mapped (not code-generated) with
  golden fixtures from the backend smoke tests.
- [Risk: large `renderedText` / PDF memory] → Mitigation: list/detail payloads
  are small; PDFs are streamed to a file, never held as a full byte buffer
  beyond download.
- [Trade-off: no offline editing] → Accepted; the workflow is inherently
  server-backed (templates live server-side).
- [Trade-off: single short-lived JWT, no refresh] → Accepted; on 401 the app
  returns to login rather than attempting a silent refresh.

## Migration Plan

1. Create `opendockify-app` Flutter project; wire CI (analyze + test).
2. Implement `flutter-app-foundation`: config, storage, API client + interceptors,
   error model, routing, theme, disclaimer.
3. Implement `flutter-app-auth`: login/register/me screens, session provider,
   logout, 401 handling.
4. Implement `flutter-app-templates`: marketplace, detail, definition parsing,
   dynamic validated form, template copy.
5. Implement `flutter-app-documents`: generate flow, list, detail, PDF
   download/open/share, re-edit, delete.
6. Implement `flutter-app-ai-assist`: polish-clause/document UI with privacy
   warning and 403/429 handling.
7. Implement `flutter-app-admin`: settings, template upsert, AI usage log.
8. Contract tests + widget tests; run `flutter analyze` clean; build APK/iOS
   smoke build; archive the change.

## Open Questions

- Android/iOS split — build for Android first and iOS next, or both in one cut?
  Default: both targets from the start (single codebase), iOS build verified in
  CI where runners permit.
- Should the app support tablets' two-pane (list + form) layout? Default: yes
  where trivial via responsive widgets, otherwise deferred.