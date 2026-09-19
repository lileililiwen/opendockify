## Context

`AiAssistService` + `AiGuard` + `OpenAiCompatibleLlmClient` call any endpoint with raw text. Platform AI adds policy routing + budgets; `HybridCacheStore` gives single-host caching without Redis ops.

## Goals / Non-Goals

Goals: local-first routing, PII scrub, token budgets, safe logs, render caching.
Non-goals: Anthropic, embeddings, Redis (see proposal).

## Decisions

- **Routing**: `IGenerationGateway`: Ollama default; OpenAI-compatible only when `Ai:Provider=openai-compatible` + HTTPS (or loopback + explicit flag).
- **Scrub**: `Ai:ScrubPii=true` default; regex + field-aware redaction (ID/phone/name) before send; re-substitute via `AiGuard.TokenizeValues`; scrubbed prompt never logged.
- **Budget**: `Ai:MaxTokensPerDay` (default 20k, 0=off); `IQuotaStore` per user; 429 with reset time; usage in `AiUsageLogs` (500-char redacted snippet).
- **Cache**: `HybridCacheStore` for `TemplateRenderer` output + LPR lookups + identical polish requests (key = SHA-256 of normalized input, never raw PII in key logs); 10-min TTL.
- **Classifier**: output legal-advice regex flags warning banner, never blocks (drafting-tool disclaimer stays).

## Risks / Trade-offs

- [Risk: scrub breaks amounts/dates] -> Mitigation: mandatory-field re-substitution is authoritative; fail-closed to original; golden tests on loan/lease templates.
- [Risk: cache serves stale LPR] -> Mitigation: LPR TTL 24h + admin invalidate endpoint.
- Licensing: no AGPL; QuestPDF MIT unchanged.

## Migration Plan

1. Add refs; wrap `AiAssistService` with gateway + policy + budget.
2. Add scrub + safe logging + classifier; add HybridCache.
3. Golden render tests + PII-leak negative tests; smoke Ollama + OpenAI-compat stub.
