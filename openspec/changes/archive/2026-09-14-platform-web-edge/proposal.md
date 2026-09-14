## Why

OpenDockify hand-rolls correlation, error mapping, and per-IP rate limits, with no CORS policy, no security headers, no API versioning, and no HTTPS/HSTS posture. `Platform.AspNetCore` + `Platform.Web.*` provide these as small opt-in packages.

## What Changes

- Adopt `Platform.AspNetCore` (correlation middleware, sanitized `ProblemDetails`, `/health` liveness alongside existing `/healthz`), `Platform.Web.Telemetry` (redaction-safe names), `Platform.Web.Cors` (named deny-by-default policy, explicit web-origin allowlist), `Platform.Web.Resilience` (AI/webhook HttpClient retry/timeout/circuit-breaker), `Platform.Web.OpenApi` (named doc registry), `Platform.Web.Versioning` (URL `/api/v1` already used; add `Api-Version` header negotiation), `Platform.Web` (security headers: HSTS, X-Content-Type-Options, frame-ancestors, referrer-policy).
- Add `ForwardedHeaders` (X-Forwarded-For/Proto) so IP rate keys work behind reverse proxy; require `Jwt:Secret` + `Sharing:HashKey` separation (startup fails if equal).
- Enforce HTTPS-only `Ai:Endpoint` unless `Ai:AllowInsecureHttp=true` (local Ollama loopback only).

## Capabilities

### New Capabilities
- `web-edge-hardening`: standard correlation/errors/headers/CORS/versioning/resilience/forwarded-headers posture.

### Modified Capabilities
None — additive middleware; existing routes and status codes preserved (error bodies gain RFC9457 `code` extension).

## Non-goals

- No OpenTelemetry exporter in this change (see observability change).
- No WAF, no DDoS protection beyond fixed-window limits.
- No breaking route renames.
- No SignalR/SSE.

## Impact

- New refs: `Platform.AspNetCore`, `Platform.Web`, `Platform.Web.Cors`, `Platform.Web.Resilience`, `Platform.Web.OpenApi`, `Platform.Web.Versioning`, `Platform.Web.Telemetry`.
- `OpenDockify.Api/Program.cs`: middleware order correlation -> forwarded-headers -> security-headers -> problem-details; CORS opt-in.
- Config: `Web:Cors:AllowedOrigins`, `Web:Hsts`, `Ai:AllowInsecureHttp=false`.
- Docs: `docs/integrations.md` CORS note; Flutter: send `X-Correlation-Id`, surface `code` from errors.
