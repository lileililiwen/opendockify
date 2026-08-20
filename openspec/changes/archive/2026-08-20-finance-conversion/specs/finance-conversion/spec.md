## ADDED Requirements

### Requirement: RMB-uppercase amount conversion

The system SHALL convert a numeric amount (in yuan) to Chinese uppercase
(中文大写) per standard financial convention, producing `元` for the integer
part, `角`/`分` for decimals, and `整` when there is no fractional part.

#### Scenario: Whole yuan

- **WHEN** the amount `1234` is converted
- **THEN** the result is `壹仟贰佰叁拾肆元整`

#### Scenario: Zero

- **WHEN** the amount `0` is converted
- **THEN** the result is `零元整`

#### Scenario: Yuan with jiao

- **WHEN** the amount `1234.5` is converted
- **THEN** the result includes `壹仟贰佰叁拾肆元伍角` and does not incorrectly
  render `整` alongside the fraction

#### Scenario: Yuan with jiao and fen

- **WHEN** the amount `1234.56` is converted
- **THEN** the result is `壹仟贰佰叁拾肆元伍角陆分`

#### Scenario: Pure fen

- **WHEN** the amount `0.05` is converted
- **THEN** the result is `伍分` (no leading `零元`)

#### Scenario: Negative amount rejected

- **WHEN** the amount `-100` is converted
- **THEN** the conversion returns an error rather than a string (invalid input
  for a money field)

#### Scenario: Decimal precision capped

- **WHEN** an amount with more than 2 decimal places is converted
- **THEN** the value is rounded to 2 decimal places per standard rounding
  before conversion

### Requirement: Interest-rate validation against LPR

The system SHALL validate an annual interest rate (in percent) against the
configurable one-year LPR reference and SHALL produce a warning-level result
(never a blocking failure) when the rate exceeds the LPR or the judicial
protection cap (4× LPR).

#### Scenario: Rate within LPR

- **WHEN** a rate is at or below the configured LPR
- **THEN** the validation result is `Ok` with no warning

#### Scenario: Rate above LPR but below cap

- **WHEN** a rate exceeds the LPR but is below 4× LPR
- **THEN** the validation result is `OverLpr` with a warning message

#### Scenario: Rate above cap

- **WHEN** a rate exceeds 4× the LPR
- **THEN** the validation result is `OverCap` with a stronger warning message,
  but the result MUST NOT prevent document generation

#### Scenario: Cap configurable

- **WHEN** the LPR reference value changes in system config
- **THEN** subsequent validations use the new LPR value (cap = 4× new LPR)

### Requirement: LPR value source

The system SHALL obtain the LPR reference value from the system configuration
(`Lpr.OneYearRate`) rather than hardcoding it.

#### Scenario: Reads configured LPR

- **WHEN** a rate is validated
- **THEN** the LPR used is the effective configured value (env > DB > default)
