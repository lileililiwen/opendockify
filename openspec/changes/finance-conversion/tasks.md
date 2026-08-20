## 1. Finance Module

- [x] 1.1 Create `src/OpenDockify.Finance`; add
  `Services/AmountToChinese.cs` — `Convert(decimal)` returning `Result<string>`;
  handles 0, whole yuan, jiao, fen, pure-fen, negative→error, rounding to 2 dp
  (`AwayFromZero`), zero-run suppression (`1001` → `壹仟零壹元整`)
- [x] 1.2 Add `Services/InterestRateValidator.cs` — `Validate(ratePct, lprPct)`
  → `RateLevel` (`Ok|OverLpr|OverCap`) + Chinese warning message; cap = 4× LPR
  named constant with legal comment
- [x] 1.3 Add adapter service `InterestRateService` that reads LPR from the
  `ISystemConfigReader` abstraction (owned by `system-config`) and delegates to
  the pure validator
- [x] 1.4 `FinanceModuleExtensions.cs`; wire into `OpenDockify.Api`

## 2. Unit Tests

- [x] 2.1 Create `tests/OpenDockify.Finance.Tests` (xUnit):
  - `0` → `零元整`
  - `1234` → `壹仟贰佰叁拾肆元整`
  - `1234.5` → `壹仟贰佰叁拾肆元伍角`
  - `1234.56` → `壹仟贰佰叁拾肆元伍角陆分`
  - `0.05` → `伍分`
  - `0.5` → `伍角`
  - `1001` → `壹仟零壹元整`
  - `1000001` → zero-run suppression correct
  - `-100` → error
  - `1.999` → rounds to `2.00` → `贰元整`
- [x] 2.2 `InterestRateValidator` tests: rate ≤ LPR → `Ok`; LPR < rate < 4×LPR
  → `OverLpr`; rate > 4×LPR → `OverCap`; boundary == 4×LPR → `OverLpr` (not
  OverCap); changed LPR changes levels

## 3. Build & Verify

- [x] 3.1 `dotnet build OpenDockify.sln` → 0 warnings / 0 errors
- [x] 3.2 `dotnet test` → all finance tests pass
- [x] 3.3 Confirm no DB/migration/endpoint changes introduced
