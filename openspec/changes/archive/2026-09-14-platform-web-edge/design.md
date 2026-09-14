## Context

`Program.cs` wires JWT/rate-limit/maintenance/correlation manually. Platform edge packages are drop-in: `AddPlatformAspNetCore`, `UsePlatformCorrelation/ProblemDetails`, `AddPlatformCors/Resilience/OpenApi/Versioning`, `Platform.Web` headers.

## Goals / Non-Goals

Goals: standard pipeline, CORS deny-default, headers, versioning, resilience, ForwardedHeaders, secret separation, HTTPS-only AI.
Non-goals: OTel exporter, WAF (see proposal).

## Decisions

- **Order**: `ForwardedHeaders` -> `Correlation` -> `SecurityHeaders` -> `ProblemDetails` -> `RateLimit` -> `Auth` -> endpoints (documented in code).
- **CORS**: `default-deny`; `Web:Cors:AllowedOrigins` explicit; automation/webhook routes never CORS-exposed beyond allowlist.
- **ProblemDetails**: map `Error.Validation`->400, `NotFound`->404, unknown->500 sanitized; add `code` extension; keep existing shapes plus extension.
- **Resilience**: AI + webhook `HttpClient`: retry 3x transient-only, 30s timeout, breaker 60s; idempotent-method guard.
- **Secrets**: startup fails if `Sharing:HashKey` missing/equals `Jwt:Secret`.

## Risks / Trade-offs

- [Risk: CORS breaks self-hosted web origin] -> Mitigation: default allow `http://localhost:8080` + docs; smoke web build.
- [Risk: ForwardedHeaders spoofing] -> Mitigation: only trust `KnownProxies/Networks` config; default loopback only.
- Licensing: no PDF/AI license impact.

## Migration Plan

1. Add refs; reorder `Program.cs`; add CORS/versioning/OpenApi registry.
2. Add secret-separation validator + AI HTTPS guard.
3. Update docs + Flutter correlation/error-code handling; smoke.
