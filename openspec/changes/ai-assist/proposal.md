## Why

The requirements call for an optional AI module that (1) polishes user-typed
custom text into standard contract clauses under template constraints and (2)
polishes the full document text without ever altering mandatory field values.
No LLM API keys may be hardcoded; the deployer supplies endpoint/key via
system config (OpenAI-compatible or Ollama), and all calls must be logged with
sensitivity warnings. If the AI toggle is off, every AI endpoint returns
disabled.

## What Changes

- `AiAssist` module with an LLM client abstraction (`ILlmClient`) supporting an
  OpenAI-compatible chat-completions endpoint (works for both OpenAI and
  Ollama's OpenAI-compatible mode). Endpoint, model, and API key come from
  `system-config` (`Ai.Enabled`, `Ai.Endpoint`, `Ai.ApiKey`, `Ai.Model`).
- Two endpoints:
  - `POST /api/ai/polish-clause` — takes user draft text + the current
    template's field/constraint context; returns polished clause text. Must not
    fabricate amounts, ID numbers, or unselected clauses.
  - `POST /api/ai/polish-document` — takes the rendered document text +
    field values; returns polished prose while preserving all mandatory field
    values byte-for-byte (guarded in code: post-process re-substitutes the
    original values).
- Strict prompt constraints baked into the module: a system prompt that
  forbids inventing figures/IDs/clauses and requires returning JSON that the
  server re-validates.
- `AiUsageLog` entity: user, action, endpoint, prompt/response snippets (with
  PII redaction), token counts if available, timestamp, success/failure.
- Gate: every AI endpoint checks `Ai.Enabled`; if disabled → `403`/`503`
  "AI disabled". Rate limit and fail-safe: on LLM error, return the original
  text unchanged with a warning (polish is best-effort).
- README + in-app warnings: do not transmit ID numbers/names to public LLMs;
  recommend local Ollama for private deployments.

## Capabilities

### New Capabilities

- `ai-assist`: toggleable LLM polish (clause + full-document), config-driven
  endpoint/key, strict anti-fabrication guards, usage logging with redaction,
  disabled-state behavior.

### Modified Capabilities

None.

## Non-goals

- No training/fine-tuning, no embeddings/RAG, no document Q&A.
- No chat UI (frontend concern; API only here).
- No automatic field filling from AI.
- No hardcoded keys or bundled LLM accounts.

## Impact

- New module `src/OpenDockify.AiAssist`: `Models/AiUsageLog.cs`,
  `Services/ILlmClient.cs`, `Services/OpenAiCompatibleLlmClient.cs`,
  `Services/PromptBuilder.cs`, `Services/AiGuard.cs` (value preservation +
  anti-fabrication), `AiAssistModuleExtensions.cs`.
- `OpenDockify.Data`: `AiUsageLog` DbSet + migration.
- Depends on `OpenDockify.SystemConfig` (read Ai.* settings) and
  `OpenDockify.Templates` (template context).
- Endpoints under `/api/ai/*`; all gated on `Ai.Enabled`.
