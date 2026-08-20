## ADDED Requirements

### Requirement: User registration

The system SHALL allow a user to register with a unique username and password;
the password SHALL be stored as a salted, iterated hash and never in plaintext.

#### Scenario: Register a new user

- **WHEN** a client POSTs a username and password to `/api/auth/register`
- **THEN** the user is created with role `Regular` and a success response with
  a JWT is returned

#### Scenario: Duplicate username rejected

- **WHEN** a client attempts to register with an existing username
- **THEN** the request fails with `409 Conflict` and no account is created

#### Scenario: Weak password rejected

- **WHEN** a client registers a password shorter than the configured minimum
- **THEN** the request fails with `400 Bad Request` and a validation message

### Requirement: Login and JWT issuance

The system SHALL authenticate a user by username/password and SHALL return a
signed JWT containing the user id and role claims; the JWT SHALL be valid only
until its configured expiry.

#### Scenario: Successful login

- **WHEN** a client POSTs valid credentials to `/api/auth/login`
- **THEN** a JWT with `sub` (user id) and `role` claims is returned and can be
  used to access authenticated endpoints

#### Scenario: Wrong credentials

- **WHEN** a client POSTs an unknown username or wrong password
- **THEN** the request fails with `401 Unauthorized` and no token is issued

#### Scenario: Expired token rejected

- **WHEN** a request carries an expired JWT
- **THEN** the API rejects it with `401 Unauthorized`

### Requirement: Roles

The system SHALL support exactly two roles — `Regular` and `Administrator` —
and SHALL enforce an administrator-only authorization policy on admin
endpoints.

#### Scenario: Regular user denied admin endpoint

- **WHEN** a user with role `Regular` calls an admin-only endpoint
- **THEN** the API returns `403 Forbidden`

#### Scenario: Administrator allowed admin endpoint

- **WHEN** a user with role `Administrator` calls an admin-only endpoint
- **THEN** the request is authorized and processed

### Requirement: Current user identity

The system SHALL expose the currently authenticated user's identity (id,
username, role) via an authenticated endpoint.

#### Scenario: Get current user

- **WHEN** a client sends a valid JWT to `/api/auth/me`
- **THEN** the API returns the user id, username, and role

#### Scenario: Unauthenticated access

- **WHEN** a client calls `/api/auth/me` without a valid JWT
- **THEN** the API returns `401 Unauthorized`

### Requirement: Default administrator seeding

The system SHALL seed an initial administrator account when the database is
empty, using credentials from configuration (overridable via environment
variables).

#### Scenario: Fresh database seeds admin

- **WHEN** the app starts against an empty database
- **THEN** a user with role `Administrator` exists with the configured seed
  credentials

#### Scenario: Seed does not duplicate on restart

- **WHEN** the app restarts against a populated database
- **THEN** no duplicate admin account is created

### Requirement: Multi-user isolation

The system SHALL scope all user-owned queries (templates, documents) to the
authenticated user's id so that a user can never observe or mutate another
user's data.

#### Scenario: Cross-user access blocked

- **WHEN** a user requests a document or template owned by a different user
- **THEN** the API returns `404 Not Found` (not the data, not a leak of
  existence)
