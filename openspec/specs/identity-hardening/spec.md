# identity-hardening Specification

## Purpose
TBD - created by archiving change platform-identity-hardening. Update Purpose after archive.
## Requirements
### Requirement: Refresh rotation and revocation

The system SHALL issue 15-minute access JWTs with rotating opaque refresh tokens; refresh reuse SHALL revoke the token family and emit an audit event.

#### Scenario: Refresh rotates

- **WHEN** a client POSTs a valid refresh token to `/api/auth/refresh`
- **THEN** a new access JWT plus a new refresh token are returned and the old refresh token is invalidated

#### Scenario: Refresh reuse detected

- **WHEN** an already-rotated refresh token is presented again
- **THEN** the API returns `401`, revokes the token family, and records a reuse audit event

#### Scenario: Logout revokes

- **WHEN** a client POSTs a valid refresh token to `/api/auth/logout`
- **THEN** the token family is revoked and subsequent refresh with it returns `401`

### Requirement: Password change and recovery

The system SHALL support authenticated password change and single-use time-boxed recovery without leaking account existence.

#### Scenario: Change password revokes sessions

- **WHEN** an authenticated user POSTs correct current plus new password to `/api/auth/change-password`
- **THEN** the password hash is updated and all refresh families for the user are revoked

#### Scenario: Recovery is enumeration-safe

- **WHEN** a client POSTs any username to `/api/auth/recovery/start`
- **THEN** the API returns `202` identically whether or not the account exists, and a single-use 30-minute token is mailed only if it exists

### Requirement: Optional TOTP two-factor

The system SHALL support opt-in TOTP 2FA with recovery codes; password-only login SHALL fail closed with a challenge when 2FA is enabled.

#### Scenario: TOTP challenge required

- **WHEN** a 2FA-enabled user POSTs correct password to `/api/auth/login`
- **THEN** the API returns a `2fa-required` challenge instead of tokens, and tokens are issued only after `POST /api/auth/2fa/verify` succeeds

### Requirement: Lockout and admin disable

The system SHALL lock out repeated failures and allow administrators to disable/enable users.

#### Scenario: Lockout after failures

- **WHEN** 5 failed logins occur for one user+IP within 15 minutes
- **THEN** further attempts return `429` until the window elapses

#### Scenario: Disabled user cannot authenticate

- **WHEN** an administrator disables a user and that user attempts login or refresh
- **THEN** the API returns `401` and no tokens are issued

