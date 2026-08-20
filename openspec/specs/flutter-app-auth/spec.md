# flutter-app-auth Specification

## Purpose
TBD - created by archiving change flutter-mobile-app. Update Purpose after archive.

## Requirements

### Requirement: User registration

The app SHALL allow a user to register with username, password, and optional
display name via `POST /api/auth/register`, and SHALL treat a successful
registration as a login (the API returns a JWT).

#### Scenario: Register and enter app

- **WHEN** the user submits valid, unique credentials on the registration screen
- **THEN** the returned JWT is stored and the user is taken to the main screen

#### Scenario: Duplicate username

- **WHEN** the API responds `409 Conflict`
- **THEN** the app shows the backend error message (e.g. username taken) and
  keeps the user on the registration screen

#### Scenario: Weak or invalid input

- **WHEN** the API responds `400 Bad Request`
- **THEN** the app shows the validation message and keeps the user on the
  registration screen

### Requirement: Login

The app SHALL authenticate via `POST /api/auth/login` and SHALL store the
returned JWT for subsequent requests.

#### Scenario: Successful login

- **WHEN** the user submits valid credentials
- **THEN** the JWT is persisted, `/api/auth/me` is used to load the profile
  (id, username, display name, role), and the user is taken to the main screen

#### Scenario: Wrong credentials

- **WHEN** the API responds `401 Unauthorized`
- **THEN** the app shows "invalid username or password" and does not navigate

#### Scenario: In-flight login state

- **WHEN** a login request is pending
- **THEN** the submit button is disabled and a progress indicator is shown to
  prevent duplicate submissions

### Requirement: Current user profile

The app SHALL load and cache the current user's profile from
`GET /api/auth/me` (id, username, display name, role, isAdministrator) and SHALL
derive role-gated UI (e.g. admin menu visibility) from it.

#### Scenario: Profile shown

- **WHEN** a logged-in user views their profile
- **THEN** the display name, username, and role are shown

#### Scenario: Admin visibility

- **WHEN** the profile's role is `Administrator`
- **THEN** admin navigation entries are visible; when it is `Regular`, they are
  hidden

### Requirement: Logout

The app SHALL provide logout that clears the stored JWT and the in-memory
profile and SHALL return the user to the login screen.

#### Scenario: Logout clears session

- **WHEN** the user taps logout
- **THEN** the token is removed from secure storage, the profile is cleared, and
  the login screen is shown

#### Scenario: Protected data gone after logout

- **WHEN** a logged-out user returns to the main screen
- **THEN** the route guard redirects them to login and no cached protected data
  is shown

### Requirement: 401 session recovery

The app SHALL treat any `401 Unauthorized` response as an expired/invalid
session: it SHALL clear the stored token and profile and SHALL route the user to
the login screen with an "session expired" message.

#### Scenario: Expired token mid-session

- **WHEN** an authenticated request returns `401`
- **THEN** the token and profile are cleared and the user is returned to login
  with a session-expired notice

### Requirement: Unauthenticated access control

All authenticated screens SHALL be guarded so that an unauthenticated user is
redirected to login before any protected data is fetched.

#### Scenario: Direct navigation without token

- **WHEN** an unauthenticated user navigates to a protected screen
- **THEN** they are redirected to login and no API call is made with a missing
  token