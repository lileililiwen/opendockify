## Context

Task 3.3 of automation-api-webhooks shipped markdown examples only. The API's
JSON contracts are already snapshot-tested, so a committed OpenAPI document can
be held to the same standard.

## Goals / Non-Goals

**Goals:** one authoritative machine-readable contract; faster first-call
success; fewer support questions on signature/replay/rate-limit failures.

**Non-Goals:** generating C# from the document; hosting Swagger UI; documenting
JWT endpoints (interactive surface).

## Decisions

- Hand-author `docs/openapi.json` (OpenAPI 3.1) covering the six automation
  paths + error schema; serve it verbatim from an endpoint that reads the
  embedded copy so file and response cannot diverge.
- Snapshot test asserts served bytes == committed file and that required paths
  are present.
- Docs: prepend a five-command quickstart; append Troubleshooting with the four
  recurring failure classes observed in review.

## Risks / Trade-offs

- [Hand-authored doc drifts from behavior] -> contract snapshot test pins the
  file; endpoint changes must touch both (same PR), enforced by review checklist.

## Migration Plan

Additive endpoint + docs; none required.

## Open Questions

- None.
