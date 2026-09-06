## ADDED Requirements
### Requirement: Enforced quality gate
The code-quality gate SHALL be enforced on every CI run.
#### Scenario: Token absent
- **WHEN** SONAR_TOKEN is not set
- **THEN** CI uses a fallback gate and fails on issues instead of silently passing
#### Scenario: Token present
- **WHEN** SONAR_TOKEN is set
- **THEN** the SonarCloud gate runs and blocks on quality issues
