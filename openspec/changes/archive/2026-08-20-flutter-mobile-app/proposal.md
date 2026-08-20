## Why

OpenDockify is a self-hosted document / contract drafting system exposed as a
REST API. Today the only client is the (planned) React SPA; there is no mobile
client. A Flutter mobile app lets users draft, fill, review, and export
documents from a phone or tablet while keeping the "self-hosted only" promise:
the app talks only to the URL the user configures, never to any third-party
service.

Flutter is chosen because it ships a single codebase for Android and iOS with a
first-class widget system for the dynamic, form-driven workflow OpenDockify
needs (typed fields, optional clauses, currency/date inputs, PDF review).

The API already covers every flow this app needs (auth, templates, document
generation, PDF download, AI polish, admin settings), so this change is a
**client-side consumer** — it adds no backend capability.

## What Changes

- A new Flutter application (separate repo, mirroring how the React SPA is
  decoupled from the backend) that consumes the OpenDockify REST API.
- Capability-by-capability coverage:
  - **Foundation**: project scaffold, API client, server URL configuration,
    secure token storage, state management, routing, theming, error handling,
    and the mandatory legal disclaimer / risk-notice UI.
  - **Auth**: register, login, `/me`, logout, session persistence, automatic
    `Authorization: Bearer` on every request, and 401 handling.
  - **Templates**: marketplace browsing, template detail, parsing of
    `DefinitionJson` (fields + clauses), and a dynamically rendered, validated
    fill-in form.
  - **Documents**: generate, paginated list, detail with rendered text, PDF
    download/open/share, re-edit (immutable history), and delete.
  - **AI assist**: polish-clause and polish-document with the mandatory
    sensitivity warning and graceful 403/429 handling.
  - **Admin**: settings browse/edit (secrets masked), admin template upsert,
    and AI usage log (all gated on the `Administrator` role).
- Test coverage for each capability (widget/unit tests plus a contract test
  against the live API where feasible).
- Documentation: a `README` for the app explaining self-hosted server
  configuration, the legal disclaimer, and build/run steps.

## Capabilities

### New Capabilities

- `flutter-app-foundation`: scaffold, API client + base-URL config, secure
  storage, state management, routing, theme, error handling, legal disclaimer.
- `flutter-app-auth`: register/login/me/logout, JWT persistence, bearer
  injection, 401 recovery.
- `flutter-app-templates`: marketplace, detail, definition parsing, dynamic
  validated form.
- `flutter-app-documents`: generate, list, detail, PDF export, re-edit, delete.
- `flutter-app-ai-assist`: clause/document polish with sensitivity warning.
- `flutter-app-admin`: settings, template upsert, AI usage log — admin only.

### Modified Capabilities

None. The backend API is consumed as-is.

## Non-goals

- **No backend changes** — this change must not touch `src/`, migrations, or
  `openspec/specs/*` backend capabilities.
- No offline-first editing (a network connection to the configured server is
  required).
- No biometric/SSO auth; no token refresh flow beyond re-login (the API issues
  a single short-lived JWT).
- No e-signature workflows (out of backend MVP scope).
- No push notifications, no analytics, no crash-reporting to third parties
  (self-hosted privacy posture).
- No desktop/web target in the first cut (Android/iOS only), though the
  architecture SHOULD not preclude adding them later.

## Impact

- New repository `opendockify-app` (Flutter/Dart) with:
  - `lib/core` (api client, config, storage, state, routing, errors),
  - `lib/features/{auth,templates,documents,ai,admin}` (feature-first layout),
  - `test/` (unit + widget tests, one contract test suite against a live API),
  - `README.md`, `analysis_options.yaml`, CI workflow mirroring the backend's
    quality gates (analyze clean, tests green).
- CI: new workflow to run `flutter analyze` and `flutter test` on push/PR.
- No changes to `OpenDockify.sln`, `src/`, or the API contract.