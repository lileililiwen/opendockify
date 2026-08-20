## 1. AiAssist Module

- [x] 1.1 Create `src/OpenDockify.AiAssist`; add `Models/AiUsageLog.cs`
  (Id, UserId, Action, RequestSnippet, ResponseSnippet, Success, Timestamp)
  and `Configuration/AiUsageLogConfiguration.cs`
- [x] 1.2 Implement `Services/ILlmClient.cs` + `OpenAiCompatibleLlmClient.cs`
  — POST `{Endpoint}/chat/completions`, Bearer `Ai.ApiKey`, model from config,
  timeout, 1 retry
- [x] 1.3 Implement `Services/PromptBuilder.cs` — system prompt forbidding
  fabricated amounts/IDs/unselected clauses; injects template fields, types,
  clause ids as context
- [x] 1.4 Implement `Services/AiGuard.cs`:
  - [x] `StripFabricated(original, input)` — strip numeric/ID patterns not in input
  - [x] `RestoreMandatoryValues(text, valuesMap)` — re-substitute EXACT original
    values for all placeholder-backed fields
- [x] 1.5 Implement `Services/AiUsageLogService.cs` — write log with redacted
  snippets (mask ID-number/name patterns)
- [x] 1.6 `AiAssistModuleExtensions.cs`; wire into `OpenDockify.Api`

## 2. Data + Migration

- [x] 2.1 Register `AiUsageLog` DbSet + configuration in `OpenDockify.Data`
- [x] 2.2 `dotnet ef migrations add AddAiUsageLogs` and apply

## 3. API Endpoints (gated)

- [x] 3.1 Shared gate: read `Ai.Enabled` from system-config; if false → 403
  "AI disabled", no LLM call
- [x] 3.2 `POST /api/ai/polish-clause` — input: draft + template context;
  output: polished clause + warning; guard applied
- [x] 3.3 `POST /api/ai/polish-document` — input: rendered text + values map +
  selected clause ids; guard `RestoreMandatoryValues`; warning when LLM fails
  (original text returned)
- [x] 3.4 Optional per-user daily rate limit via `Ai.RateLimitPerDay` (default
  off)

## 4. Docs & Warnings

- [x] 4.1 README section: do not transmit ID numbers/names to public LLMs;
  recommend local Ollama; how to configure endpoint/key/model; how to disable
- [x] 4.2 API responses include a `warning` field reminding of sensitivity

## 5. Build & Verify

- [x] 5.1 `dotnet build OpenDockify.sln` → 0 warnings / 0 errors
- [x] 5.2 Unit tests for `AiGuard`:
  - [x] fabricated amount in LLM output stripped
  - [x] fabricated 18-digit ID stripped
  - [x] mandatory values restored byte-for-byte (uppercase amount + name)
  - [x] unselected clause content absent
- [x] 5.3 HTTP smoke tests against local mock/Ollama:
  - [x] `Ai.Enabled=false` → 403, no external call (mock asserts no hit)
  - [x] enabled → polish-clause returns text; polish-document preserves values
  - [x] LLM down → original text + warning, log row with Success=false
  - [x] usage logs written with redacted snippets; admin can list them