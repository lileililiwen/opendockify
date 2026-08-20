## ADDED Requirements

### Requirement: Stored system settings

The system SHALL persist global settings in a database table keyed by name,
each value stored as JSON, and SHALL expose typed read/write access to modules.

#### Scenario: Read a typed setting

- **WHEN** a module reads the setting `Lpr.OneYearRate` through the config
  service
- **THEN** the stored JSON value is returned typed as a decimal

#### Scenario: Write a setting

- **WHEN** an administrator updates a setting value
- **THEN** the new value is persisted and later reads return the new value

### Requirement: Setting allowlist

The system SHALL only accept settings whose key is in a documented allowlist
and SHALL reject unknown keys.

#### Scenario: Unknown key rejected

- **WHEN** an administrator tries to set a key not in the allowlist
- **THEN** the API returns `400 Bad Request` and nothing is persisted

#### Scenario: Known keys accepted

- **WHEN** an administrator updates any of `Ai.Enabled`, `Ai.Endpoint`,
  `Ai.ApiKey`, `Ai.Model`, `Lpr.OneYearRate`, `Lpr.ReferenceDate`
- **THEN** the update is accepted

### Requirement: Environment override precedence

The system SHALL resolve a setting's effective value with precedence:
environment variable (if set) > database value > default.

#### Scenario: Env overrides DB

- **WHEN** `LPR_ONE_YEAR_RATE` (or the documented env mapping) is set and a
  different value exists in the DB
- **THEN** the effective value is the environment variable's value

#### Scenario: DB value when no env

- **WHEN** no environment variable is set for a key
- **THEN** the database value is used

#### Scenario: Default when neither exists

- **WHEN** neither an env variable nor a DB row exists for a key
- **THEN** the code-defined default is returned

### Requirement: API key secrecy

The system SHALL never return a stored API key in full, SHALL mask it in admin
reads, and SHALL not log it.

#### Scenario: Masked on read

- **WHEN** an administrator reads settings
- **THEN** `Ai.ApiKey` is returned masked (e.g. `••••` suffix), never the full
  value

#### Scenario: Not logged

- **WHEN** the system logs configuration activity
- **THEN** the API key value does not appear in any log line

### Requirement: Admin settings API

The system SHALL expose admin-only endpoints to read all settings and update a
setting by key, applying validation per key type.

#### Scenario: Admin reads settings

- **WHEN** an authenticated Administrator calls `GET /api/admin/settings`
- **THEN** all settings are returned with secrets masked

#### Scenario: Regular user denied

- **WHEN** a Regular user calls a settings endpoint
- **THEN** the API returns `403 Forbidden`

### Requirement: Default seeding

The system SHALL seed default settings (`Ai.Enabled=false`, default LPR value)
when the database is empty.

#### Scenario: Fresh DB has defaults

- **WHEN** the app starts against an empty database
- **THEN** the settings table contains `Ai.Enabled=false` and a default
  `Lpr.OneYearRate`
