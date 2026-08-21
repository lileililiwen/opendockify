## MODIFIED Requirements

### Requirement: User registration

The system SHALL allow public registration only when `Auth:AllowRegistration` is
enabled; accepted accounts SHALL use unique usernames, SHALL store passwords as
salted iterated hashes, and SHALL be assigned role `Regular`.

#### Scenario: Register a new user

- **WHEN** public registration is enabled and a client POSTs a unique username and
  valid password to `/api/auth/register`
- **THEN** the user is created with role `Regular` and a success response with a
  JWT is returned

#### Scenario: Registration disabled

- **WHEN** public registration is disabled and a client POSTs to
  `/api/auth/register`
- **THEN** the API returns `403 Forbidden` and no account is created

#### Scenario: Duplicate username rejected

- **WHEN** a client attempts to register with an existing username
- **THEN** the request fails with `409 Conflict` and no account is created

#### Scenario: Weak password rejected

- **WHEN** a client registers a password shorter than the configured minimum
- **THEN** the request fails with `400 Bad Request` and a validation message

#### Scenario: Registration rate limited

- **WHEN** one client exceeds the configured registration-attempt limit within an
  hour
- **THEN** additional registration requests return `429 Too Many Requests` without
  invoking account creation

### Requirement: Login and JWT issuance

The system SHALL authenticate a user by username/password and SHALL return a
signed JWT containing the user id and role claims; the JWT SHALL be valid only
until its configured expiry, and login attempts SHALL be rate limited per client.

#### Scenario: Successful login

- **WHEN** a client POSTs valid credentials to `/api/auth/login`
- **THEN** a JWT with `sub` (user id) and `role` claims is returned and can be used
  to access authenticated endpoints

#### Scenario: Wrong credentials

- **WHEN** a client POSTs an unknown username or wrong password
- **THEN** the request fails with `401 Unauthorized` and no token is issued

#### Scenario: Expired token rejected

- **WHEN** a request carries an expired JWT
- **THEN** the API rejects it with `401 Unauthorized`

#### Scenario: Login rate limited

- **WHEN** one client exceeds the configured login-attempt limit within a minute
- **THEN** additional login requests return `429 Too Many Requests` without
  validating credentials

### Requirement: Default administrator seeding

The system SHALL seed an initial administrator account when the database is empty
using explicitly configured credentials; production MUST NOT fall back to known
default credentials.

#### Scenario: Fresh database seeds admin

- **WHEN** the app starts against an empty database with valid configured seed
  credentials
- **THEN** a user with role `Administrator` exists with those credentials

#### Scenario: Seed does not duplicate on restart

- **WHEN** the app restarts against a populated database
- **THEN** no duplicate administrator account is created

#### Scenario: Production defaults prohibited

- **WHEN** production configuration omits seed credentials or uses the documented
  development password
- **THEN** startup is rejected before the seeder runs
