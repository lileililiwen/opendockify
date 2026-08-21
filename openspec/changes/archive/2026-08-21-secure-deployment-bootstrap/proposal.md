## Why

The documented Docker quick start can start with known administrator credentials,
does not provide a production JWT secret, and declares a health check whose tool is
absent from the runtime image. Authentication endpoints are also unthrottled and
registration cannot be disabled, so a default self-hosted deployment is not safe
enough to expose beyond a trusted development network.

## What Changes

- Validate production JWT and administrator seed credentials before database
  migration or HTTP serving begins.
- Make public registration explicitly configurable and disabled in the production
  container by default.
- Apply built-in per-client rate limits to registration and login endpoints and
  return a standard `429` response when the limit is exceeded.
- Supply an example deployment environment file and require secrets in the Docker
  Compose configuration instead of silently using known defaults.
- Make the container health check executable by the runtime image.
- Update deployment and security documentation to describe the secure bootstrap.

## Capabilities

### New Capabilities

- `secure-deployment-bootstrap`: Production configuration validation and a
  functional, secret-driven Docker bootstrap and health check.

### Modified Capabilities

- `user-auth`: Configurable public registration, authentication endpoint rate
  limiting, and production-safe administrator seeding.

## Non-goals

- No invitation workflow, email verification, password reset, SSO, or expanded
  role model.
- No automatic generation or storage of secrets inside the application database.
- No reverse-proxy, TLS certificate, firewall, or public SaaS provisioning.

## Impact

- `OpenDockify.Api` startup, authentication endpoints, configuration, and rate
  limiting middleware.
- `OpenDockify.Data` administrator seeding behavior.
- Docker image, Compose configuration, example environment values, and README.
- Unit tests for production configuration validation and registration policy.
