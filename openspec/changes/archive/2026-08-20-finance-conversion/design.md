## Context

Pure business logic module, no persistence, no endpoints. Two services:

1. `AmountToChinese.Convert(decimal amount)` — standard 中文大写 algorithm.
   Splits into integer + two decimals, maps digits to 零壹贰叁肆伍陆柒捌玖 and
   units 仟佰拾/万亿 for the integer part, then 角/分 for fractions. Rules:
   - `0` → `零元整`; negative → error (money fields are validated non-negative
     upstream).
   - No fraction → append `整`; fraction present → append `X角Y分` (omit
     `零角`, handle `0.05` → `伍分`, `0.5` → `伍角`, `0.1` → `壹角`).
   - Round to 2 dp (banker's or standard? — standard `MidpointRounding.AwayFromZero`
     per finance convention) before converting.
   - Zero-suppression for internal zero runs in the integer part (e.g.
     `1001` → `壹仟零壹元整`).

2. `InterestRateValidator.Validate(decimal annualRatePct, decimal lprPct)` —
   compares the rate to LPR and to `cap = 4 × lprPct`. Returns an enum
   `RateLevel { Ok, OverLpr, OverCap }` plus a human-readable message in
   Chinese. This is advisory only: callers (generation UI) surface a warning
   banner but never block PDF creation. The cap multiplier is a named constant
   `JudicialProtectionCapMultiplier = 4.0m` documented with the current rule
   and kept configurable-in-code.

The LPR value is injected via `ISystemConfigReader` interface (defined in
`system-config`) so the module has no hard DB dependency — it depends on the
abstraction only.

## Goals / Non-Goals

**Goals:**
- Correct RMB-uppercase conversion for all edge cases with tests.
- LPR-relative interest warning levels.
- Consumable from both Templates (renderer) and Generation (validation).

**Non-Goals:**
- Any persistence/endpoints.
- Currency other than CNY.
- Legal determination of the cap (it's config-driven with a documented default).

## Decisions

- **`AmountToChinese.Convert` returns `Result<string>`** (value or error) so
  negative/overflow input surfaces as an error, not silent text.
- **Rounding**: `MidpointRounding.AwayFromZero` to 2 dp — matches standard
  financial rounding used in Chinese banking forms.
- **`InterestRateValidator` is a pure static function** with LPR passed in;
  a thin service adapter pulls LPR from system-config. Keeps the core unit
  testable with no DI.
- **Cap constant documented + configurable**: `4.0m` multiplier lives in the
  service with a comment referencing 最高法民间借贷司法解释; changing the rule
  is a one-line code change, deliberately not a DB setting for MVP.

## Risks / Trade-offs

- [Risk: uppercase conversion bugs in rare digit runs] → Mitigation: extensive
  unit tests for 0, 10, 1001, 1000001, decimals, jiao/fen-only cases.
- [Risk: LPR stale in DB] → Mitigation: env override + admin-editable value +
  `Lpr.ReferenceDate` recorded; README advises periodic updates.
- [Risk: cap rule changes by law] → Mitigation: documented constant + warning
  text notes it is advisory; deployer can adjust.

## Migration Plan

1. Add `OpenDockify.Finance` module with the two services.
2. Add the `ISystemConfigReader` abstraction (owned by `system-config`) and
   inject into the validator adapter.
3. Write unit tests (xUnit) for all AmountToChinese edge cases and
   InterestRateValidator levels.
4. No migration/endpoints. Verify: `dotnet build` 0/0; `dotnet test` green.

## Open Questions

- Banker's vs standard rounding: standard (`AwayFromZero`) chosen; note in
  comment for review.
- Should the cap multiplier become a DB setting? Deferred; constant for MVP.
