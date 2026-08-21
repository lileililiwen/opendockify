## 1. Tests First

- [x] 1.1 Add unit tests for accepted development/production bootstrap
  configuration and every production rejection scenario
- [x] 1.2 Add unit tests for registration enablement and positive rate-limit
  configuration parsing
- [x] 1.3 Define HTTP smoke checks for disabled registration and authentication
  throttling

## 2. Security Bootstrap

- [x] 2.1 Implement reusable production security configuration validation and
  invoke it before application build, migration, or seeding
- [x] 2.2 Remove production administrator defaults while preserving explicit
  development-only defaults
- [x] 2.3 Gate public registration with `Auth:AllowRegistration` and return a clear
  forbidden response when disabled
- [x] 2.4 Add per-client fixed-window policies for login and registration and wire
  the rate-limiting middleware

## 3. Container Deployment

- [x] 3.1 Require JWT and administrator secrets in Docker Compose and default public
  registration to disabled
- [x] 3.2 Add a non-secret `.env.example` describing required and optional values
- [x] 3.3 Make the image health-check dependency available in the runtime image
- [x] 3.4 Update README deployment and security guidance

## 4. Verify and Deliver

- [x] 4.1 Validate the OpenSpec change strictly and run formatting/diff checks
- [x] 4.2 Build with zero warnings/errors and run the complete automated test suite
- [x] 4.3 Start the configured API and execute the HTTP security smoke scenarios
- [x] 4.4 Archive the completed change and commit only its related paths
