# Proposal: Make the quality gate mandatory
## Why
The SonarCloud quality gate is only active when SONAR_TOKEN is set (if: env.SONAR_TOKEN != ''); without the secret the gate silently no-ops, giving a false sense of quality.
## What Changes
- Make the quality gate enforced: require the token or add a non-Sonar bug/smell linter, and fail loudly when the gate is absent.

## Capabilities
### New Capabilities
- `od-require-sonar-gate`: the code-quality gate is enforced on every CI run, not silently skipped.

### Modified Capabilities
None.

## Impact
Affects: .github/workflows/ci.yml.
