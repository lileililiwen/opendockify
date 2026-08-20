## ADDED Requirements

### Requirement: Flutter project scaffold

The app SHALL be a Flutter application targeting Android and iOS with a
feature-first source layout (`lib/core` for infrastructure and
`lib/features/<feature>` per capability) and SHALL keep all API interaction
behind a single typed client in `lib/core/api`.

#### Scenario: Project builds

- **WHEN** a developer runs `flutter analyze` and `flutter test` on the app
- **THEN** analysis completes with zero issues and all tests pass

#### Scenario: Feature boundaries

- **WHEN** a new screen is added
- **THEN** it is placed under the matching `lib/features/<feature>` and reuses
  the core API client rather than issuing its own HTTP calls

### Requirement: Server base URL configuration

The app SHALL let the user configure the self-hosted server base URL at runtime
and SHALL persist it across launches. The app SHALL NOT hardcode or default to
any third-party/SaaS URL.

#### Scenario: Configure server on first launch

- **WHEN** the app starts with no stored base URL
- **THEN** a connection screen is shown where the user enters the server URL

#### Scenario: Validate connectivity

- **WHEN** the user saves a base URL
- **THEN** the app calls `/healthz` and only accepts the URL when the server
  responds with status `ok`, otherwise it shows an inline error and keeps the
  previous value

#### Scenario: Reset configuration

- **WHEN** the user resets the connection settings
- **THEN** the stored base URL and session are cleared and the app returns to
  the connection screen

### Requirement: Typed API client

The app SHALL expose a single typed API client that models every backend
endpoint the app uses, maps JSON DTOs to typed models, and normalizes failures
into a typed `ApiError` (status, code, message) surfaced through one error
presentation helper.

#### Scenario: Successful request

- **WHEN** an API method returns 2xx
- **THEN** the client returns the typed model and no error is surfaced

#### Scenario: Failed request

- **WHEN** an API method returns a non-2xx status
- **THEN** the client raises a typed `ApiError` whose message is taken from the
  response body's `error` field when present, and the UI shows a friendly,
  non-technical error message

#### Scenario: Network failure

- **WHEN** the request cannot reach the server (DNS, refused, timeout)
- **THEN** the UI shows a "cannot reach server — check the connection settings"
  message and offers a path to the connection screen

### Requirement: Mandatory legal disclaimer

The app SHALL display the OpenDockify legal disclaimer (drafting tool only; not
legal advice; no guarantee of legal validity; predatory-lending prohibition) on
first launch and in an in-app "About / Legal" screen, and SHALL NOT remove it.

#### Scenario: First launch shows disclaimer

- **WHEN** a user opens the app for the first time
- **THEN** the legal disclaimer is shown before any drafting workflow is
  available

#### Scenario: Disclaimer accessible later

- **WHEN** a user opens the About / Legal screen
- **THEN** the full disclaimer text is displayed

### Requirement: Risk notice on generated documents

The app SHALL surface each template's `riskNoticeText` (when present) on the
document form screen and SHALL display the rendered document's risk notice
together with the generated text.

#### Scenario: Risk notice on form

- **WHEN** a user opens a template's fill-in form
- **THEN** the template's risk notice text is shown above the form fields

#### Scenario: Risk notice on generated document

- **WHEN** a document is generated
- **THEN** the rendered text shown to the user includes the risk notice the
  backend embedded in the document

### Requirement: Session and token storage

The app SHALL store the JWT in the platform secure storage (Keychain /
Keystore), SHALL load it into memory at startup, and SHALL attach it as
`Authorization: Bearer <token>` on every authenticated request. Tokens SHALL
NOT be written to logs, plaintext preferences, or crash reports.

#### Scenario: Token persisted across restarts

- **WHEN** the user logs in and later restarts the app
- **THEN** the stored token is restored and the user is not asked to log in
  again until the token expires

#### Scenario: Token never logged

- **WHEN** a request fails or succeeds
- **THEN** the token never appears in logs, error messages, or analytics

### Requirement: Theme and navigation

The app SHALL provide a consistent Material 3 theme (light/dark) and SHALL use
declarative routing with route guards: unauthenticated users are redirected to
login, and admin routes require the `Administrator` role.

#### Scenario: Unauthenticated deep link

- **WHEN** an unauthenticated user opens a protected route
- **THEN** they are redirected to the login screen, and after login they return
  to the originally requested route

#### Scenario: Non-admin blocked from admin routes

- **WHEN** a `Regular` user opens an admin route
- **THEN** the route is refused and a "requires administrator" message is shown

### Requirement: Localization and formatting

The app SHALL render currency fields and generated amounts using the device
locale where applicable and SHALL pass numeric/currency/date values to the API
in the format the backend expects (invariant decimal strings for amounts,
`yyyy-MM-dd` for dates).

#### Scenario: Date field submission

- **WHEN** a user picks a date for a `Date` field
- **THEN** the value is submitted to the API as `yyyy-MM-dd`

#### Scenario: Currency field submission

- **WHEN** a user enters a currency amount
- **THEN** the value is submitted as an invariant decimal string (no currency
  symbol or thousands separators)