## Why

AI polish sends raw PII to any configured endpoint with only a warning, has no per-token budget, keeps 2000-char log snippets, and allows `http:` endpoints. Platform AI + Caching packages provide policy-gated generation and safe local caching to cut cost and leakage.

## What Changes

- Adopt `Platform.Ai.Contracts` + `Platform.Ai` (policy-gated generation, routing) + `Platform.Ai.Ollama` (default, local, no data egress) + `Platform.Ai.OpenAiCompatible` (optional, HTTPS-only unless loopback) + `Platform.Ai.Testing` fakes; keep `Ai.Enabled=false` default.
- Add pre-LLM PII scrub option (`Ai:ScrubPii=true` default: ID/phone redaction before send, re-substitution via existing `AiGuard`), per-user per-day token budget (`Ai:MaxTokensPerDay`), output legal-advice classifier warning, log truncation 500 chars with `\d{6,}` redaction.
- Adopt `Platform.Caching` + `Platform.Caching.Hybrid` (single-host, template-render + LPR + polish-cache with tenant/app prefixes, redaction-safe telemetry); Redis explicitly NOT adopted (single-host constraint).
- Keep mechanical guards (`AiGuard.TokenizeValues/StripFabricated/RemoveUnselectedClauseTitles`); fail-closed to original text.

## Capabilities

### New Capabilities
- `ai-caching`: policy-gated local-first AI with PII scrub, token budgets, safe logs, plus HybridCache for renders.

### Modified Capabilities
None — existing `/api/ai/polish-*` routes preserved; scrub/budget/cache additive with safe defaults.

## Non-goals

- No Anthropic adapter (only Ollama + OpenAI-compatible).
- No embeddings/vector search in this change.
- No Redis deployment.
- No change to legal disclaimer (drafting tool only, no legal validity).

## Impact

- New refs: `Platform.Ai`, `Platform.Ai.Contracts`, `Platform.Ai.Ollama`, `Platform.Ai.OpenAiCompatible`, `Platform.Caching`, `Platform.Caching.Hybrid`.
- `OpenDockify.AiAssist`: route through `IGenerationGateway` with policy; `Generation`/`Finance`: cache LPR/renders; config `Ai:ScrubPii`, `Ai:MaxTokensPerDay`, `Cache:*`.
- Docker: optional Ollama sidecar documented, not added to compose (deployer-owned).
- Flutter: token-budget surface in AI dialog (remaining quota), scrub toggle hint.
