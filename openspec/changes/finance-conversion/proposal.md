## Why

Two finance-specific behaviors are core requirements: (1) currency fields must
automatically render an RMB-uppercase (中文大写) amount — e.g. `1234` becomes
`壹仟贰佰叁拾肆元整` — with edge cases for zero and decimals; (2) the annual
interest-rate field must be validated against the LPR reference value with a
warning (not a block) when it exceeds the judicial-protection cap (4× LPR).
This change isolates that logic so both the template engine and the generation
flow can share it.

## What Changes

- `OpenDockify.Finance` module with:
  - `AmountToChinese.Convert(decimal) -> string`: full RMB-uppercase
    conversion (integer + jiao/fen), handling `0`, negative amounts, and
    decimals (0.05 → `伍分`, 0.1 → `壹角`, rounding to 2 dp per convention).
  - `InterestRateValidator.Validate(decimal annualRatePct, decimal lprPct) ->
    ValidationResult`: returns a warning level (ok | over-lpr | over-cap) based
    on the configured LPR; cap = 4× LPR per the current judicial protection
    rule; never blocks generation, only warns.
  - Tests covering the edge cases (zero, whole yuan, yuan+jiao, yuan+jiao+fen,
    pure fen, negative, rounding).
- The LPR reference value comes from `system-config` (`Lpr.OneYearRate`),
  resolved with env override, not hardcoded.

## Capabilities

### New Capabilities

- `finance-conversion`: RMB-uppercase amount conversion (edge cases) and
  interest-rate validation against the configurable LPR with warning-level
  results.

### Modified Capabilities

None.

## Non-goals

- No tax/currency-exchange or VAT calculation.
- No legal advice on interest caps — the module only computes the comparison
  and the warning text; the exact cap rule is configurable.
- No locale-generalized number-to-words (Chinese uppercase only).

## Impact

- New module `src/OpenDockify.Finance`: `Services/AmountToChinese.cs`,
  `Services/InterestRateValidator.cs`, `FinanceModuleExtensions.cs`.
- Referenced by `OpenDockify.Templates` (renderer) and
  `OpenDockify.Generation` (validation + warning surfaced in UI/PDF).
- Consumes `Lpr.OneYearRate` and `Lpr.ReferenceDate` from
  `OpenDockify.SystemConfig`.
- Pure logic module — no DB, no endpoints, no migration.
