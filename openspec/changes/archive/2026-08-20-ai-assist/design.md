## Context

AI is strictly optional and security-conscious. Design pillars:

1. **Gate first**: `Ai.Enabled` (from `system-config`) short-circuits all AI
   endpoints. No key, no endpoint → disabled. This satisfies "if disabled in
   system config, all AI functions inactive."

2. **Client**: `OpenAiCompatibleLlmClient` posts to `{Ai.Endpoint}/chat/completions`
   with `Ai.ApiKey` (Bearer) and `Ai.Model`. Works for OpenAI and Ollama's
   OpenAI-compatible API. Timeout configured; retries limited (1 retry).

3. **Anti-fabrication guard (`AiGuard`)**:
   - Clause polish: the system prompt explicitly forbids inventing amounts,
     ID numbers, dates, or unselected clauses. Server-side post-processing
     scans the response for numeric/ID patterns not present in the input and
     strips/redacts them before returning.
   - Document polish: after the LLM returns, the guard re-inserts the EXACT
     original values for every placeholder-backed field (values map from the
     generation snapshot), so field values can never drift even if the LLM
     alters them. This is a hard, mechanical guarantee — not prompt reliance.

4. **Logging**: `AiUsageLog` stores user/action/timestamp/status + redacted
   snippets. Redaction masks 18-digit ID-number patterns and name tokens before
   persisting. Logs are visible to admins only.

5. **Sensitivity warnings**: README + a `Warning` field returned in API
   responses remind users not to send ID numbers/names to public LLMs and to
   prefer local Ollama.

Prompt templates live in `PromptBuilder` and include the template's declared
field names + types + clause ids as context so the LLM stays within the
template's constraints.

## Goals / Non-Goals

**Goals:**
- Toggle-gated polish for clauses and full documents.
- Config-driven endpoint/key/model (OpenAI + Ollama compatible).
- Mechanical preservation of mandatory field values.
- Usage logging with redaction; best-effort failure.

**Non-Goals:**
- Chat UI, RAG/embeddings, document Q&A, auto-fill.
- Streaming responses (MVP returns complete JSON).
- Key rotation/management (deployer-owned).

## Decisions

- **OpenAI-compatible protocol** as the single integration surface (Ollama
  supports it) — avoids two client implementations.
- **Guard is mechanical, not prompt-only**: mandatory values are re-substituted
  post-LLM; fabricated numbers are stripped by pattern matching against the
  input. Prompt constraints are defense-in-depth.
- **Non-blocking failures**: original text + warning returned on LLM error.
- **Log redaction before persist**: never store raw ID numbers; document the
  residual risk of sending data to a third-party LLM in README.

## Risks / Trade-offs

- [Risk: sending sensitive data to a public LLM] → Mitigation: README + in-app
  warning, redaction, Ollama recommendation; deployment is self-hosted so the
  operator controls exposure.
- [Risk: LLM still paraphrases a field value despite guard] → Mitigation:
  mechanical re-substitution from the snapshot makes this impossible for
  placeholder-backed values.
- [Risk: API key leakage in logs] → Mitigation: never log the key; redaction
  guard; `Ai.ApiKey` masked by `system-config`.
- [Risk: cost/abuse of the endpoint] → Mitigation: optional per-user daily
  rate limit (config `Ai.RateLimitPerDay`, default off), documented.

## Migration Plan

1. Add `OpenDockify.AiAssist` module: entities, `ILlmClient`, OpenAI-compatible
   client, `PromptBuilder`, `AiGuard`, `AiUsageLogService`.
2. Register `AiUsageLog` DbSet in `OpenDockify.Data`; migration `AddAiUsageLogs`.
3. Add endpoints `/api/ai/polish-clause`, `/api/ai/polish-document` gated on
   `Ai.Enabled`.
4. Wire `system-config` settings; add optional rate-limit setting.
5. Update README with sensitivity warnings + Ollama guidance.
6. Verify: disabled → 403/503 no external call; enabled → polish works against
   a local Ollama or mock; guard unit tests for value preservation and
   fabrication stripping; log rows written + redacted.

## Open Questions

- Should `Ai.Enabled` be set automatically when an endpoint+key are first
  saved? Decision: no — admin explicitly enables; prevents surprise external
  calls.
- Streaming vs non-streaming: non-streaming for MVP (simpler, fits JSON
  guard).
