## 1. Project Scaffold & CI

- [x] 1.1 Create Flutter project `opendockify-app` (org `org.opendockify.app`,
  platforms android,ios), set display name "OpenDockify", MIT license
- [x] 1.2 Feature-first layout: `lib/core/{api,config,storage,state,routing,
  theme,errors}`, `lib/features/{auth,templates,documents,ai,admin}`
- [x] 1.3 Add dependencies: `dio`, `flutter_riverpod`, `riverpod_generator`,
  `go_router`, `flutter_secure_storage`, `shared_preferences`, `share_plus`,
  `open_filex`
- [x] 1.4 Add CI workflow: `flutter pub get`, `flutter analyze` (0 issues),
  `flutter test`, format check; Android build smoke + iOS build where runner
  permits
- [x] 1.5 `analysis_options.yaml` with `flutter_lints`; zero-warning policy
- [x] 1.6 App README: self-hosted server config, legal disclaimer, build/run

## 2. Foundation

- [x] 2.1 Config service: base URL read/write via `shared_preferences`; initial
  empty state; reset clears URL + session
- [x] 2.2 Connection screen: base URL input, `/healthz` probe, inline errors,
  shown on first launch
- [x] 2.3 Typed API client (`ApiClient`): all endpoints modeled, DTO→model
  mapping, `ApiError` type (status/code/message), error-presentation helper
- [x] 2.4 Dio interceptors: `AuthInterceptor` (bearer injection),
  `ErrorInterceptor` (normalize errors, emit 401 event)
- [x] 2.5 Token storage: `flutter_secure_storage` (Keychain/Keystore); token
  loaded into memory at startup; never logged
- [x] 2.6 Routing via `go_router` with guards (auth required; admin requires
  role); deep-link return after login
- [x] 2.7 Material 3 theme (light/dark) + app shell with navigation
- [x] 2.8 Legal disclaimer screen (first launch + About/Legal), risk-notice
  component used on template form and generated document screens
- [x] 2.9 Date (`yyyy-MM-dd`) and currency (invariant decimal) formatting
  helpers

## 3. Auth

- [x] 3.1 Login screen + flow: `POST /api/auth/login`, persist JWT, load
  `/api/auth/me` profile, navigate to main; 401 → inline error; in-flight
  disable
- [x] 3.2 Registration screen + flow: `POST /api/auth/register`, treat success
  as login; surface 409/400 messages
- [x] 3.3 Profile provider (id, username, display name, role, isAdministrator)
- [x] 3.4 Logout: clear secure token + profile, return to login
- [x] 3.5 401 handling: clear session, route to login with "session expired"
- [x] 3.6 Session provider drives route guards and admin visibility

## 4. Templates

- [x] 4.1 Marketplace screen: `GET /api/templates/marketplace`, category
  grouping/filter, refresh, error/empty states
- [x] 4.2 Template detail screen: `GET /api/templates/{id}`, metadata + body
  preview + risk notice
- [x] 4.3 `TemplateDefinitionParser`: parse `definitionJson` → fields + clauses;
  malformed JSON → typed parse error (no crash)
- [x] 4.4 `DynamicTemplateForm`: per-type inputs (text/number/currency/date),
  required markers, clause toggles, client-side validation mirroring backend
  rules
- [x] 4.5 Generate request builder: `values` map (typed → invariant strings) +
  `selectedClauseIds`
- [x] 4.6 Copy template action (`POST /api/templates/{id}/copy`)
- [x] 4.7 Private template create/edit/delete screens with backend validation
  surfacing
- [x] 4.8 Interest-rate warning display from generation result (non-blocking)

## 5. Documents

- [x] 5.1 Document list: `GET /api/documents?page=&pageSize=`, paging, refresh,
  empty state
- [x] 5.2 Generate flow: submit form → show rendered text, warnings, risk
  notice, download action; handle 400/403/404
- [x] 5.3 Document detail: `GET /api/documents/{id}` (rendered text + metadata)
- [x] 5.4 PDF download: absolute-ize `downloadUrl`, download to documents dir,
  open via platform viewer / share sheet; failure + retry
- [x] 5.5 Re-edit: `POST /api/documents/{id}/reedit` creates new version,
  navigate to it; original preserved
- [x] 5.6 Delete with confirmation dialog (`DELETE /api/documents/{id}`)

## 6. AI Assist

- [x] 6.1 Clause polish: `POST /api/ai/polish-clause`, review/accept/discard
- [x] 6.2 Document polish: `POST /api/ai/polish-document`, review flow
- [x] 6.3 Privacy/sensitivity warning shown before first AI use
- [x] 6.4 403 (disabled) and 429 (rate limit) handling with specific messages,
  no auto-retry

## 7. Admin

- [x] 7.1 Admin route guard + navigation visibility (role-based)
- [x] 7.2 Settings screen: `GET /api/admin/settings` (secrets masked),
  `PUT /api/admin/settings/{key}`, invalid-value handling
- [x] 7.3 Admin template upsert screen: `POST /api/admin/templates` (optional
  id for update)
- [x] 7.4 AI usage log screen: `GET /api/admin/ai-usage?limit=`
- [x] 7.5 Admin error surfacing + 403 role recovery

## 8. Tests & Verification

- [x] 8.1 Unit tests: definition parser, validation, currency/date formatting,
  DTO mapping (golden fixtures from backend responses)
- [x] 8.2 Widget tests per feature: login, register, marketplace, dynamic form,
  generate, document list, PDF action, admin settings
- [x] 8.3 Contract tests: run against a live local API instance
  (Docker `docker compose up`), covering auth → marketplace → generate →
  download → re-edit → delete happy path and negative cases (wrong password,
  404 foreign resource, non-admin 403, AI-disabled 403)
- [x] 8.4 `flutter analyze` → 0 issues; `flutter test` → all green
- [x] 8.5 Android debug build + iOS build smoke
- [x] 8.6 Update `tasks.md` — all boxes checked
- [x] 8.7 Archive: `openspec archive flutter-mobile-app -y`