## 1. AiAssist Module

- [ ] 1.1 Create `src/OpenDockify.AiAssist`; add `Models/AiUsageLog.cs`
  (Id, UserId, Action, RequestSnippet, ResponseSnippet, Success, Timestamp)
  and `Configuration/AiUsageLogConfiguration.cs`
- [ ] 1.2 Implement `Services/ILlmClient.cs` + `OpenAiCompatibleLlmClient.cs`
  — POST `{Endpoint}/chat/completions`, Bearer `Ai.ApiKey`, model from config,
  timeout, 1 retry
- [ ] 1.3 Implement `Services/PromptBuilder.cs` — system prompt forbidding
  fabricated amounts/IDs/unselected clauses; injects template fields, types,
  clause ids as context
- [ ] 1.4 Implement `Services/AiGuard.cs`:
  - `StripFabricated(original, input)` — strip numeric/ID patterns not in input
  - `RestoreMandatoryValues(text, valuesMap)` — re-substitute EXACT original
    values for all placeholder-backed fields
- [ ] 1.5 Implement `Services/AiUsageLogService.cs` — write log with redacted
  snippets (mask ID-number/name patterns)
- [ ] 1.6 `AiAssistModuleExtensions.cs`; wire into `OpenDockify.Api`

## 2. Data + Migration

- [ ] 2.1 Register `AiUsageLog` DbSet + configuration in `OpenDockify.Data`
- [ ] 2.2 `dotnet ef migrations add AddAiUsageLogs` and apply

## 3. API Endpoints (gated)

- [ ] 3.1 Shared gate: read `Ai.Enabled` from system-config; if false → 403
  "AI disabled", no LLM call
- [ ] 3.2 `POST /api/ai/polish-clause` — input: draft + template context;
  output: polished clause + warning; guard applied
- [ ] 3.3 `POST /api/ai/polish-document` — input: rendered text + values map +
  selected clause ids; guard `RestoreMandatoryValues`; warning when LLM fails
  (original text returned)
- [ ] 3.4 Optional per-user daily rate limit via `Ai.RateLimitPerDay` (default
  off)

## 4. Docs & Warnings

- [ ] 4.1 README section: do not transmit ID numbers/names to public LLMs;
  recommend local Ollama; how to configure endpoint/key/model; how to disable
- [ ] 4.2 API responses include a `warning` field reminding of sensitivity

## 5. Build & Verify

- [ ] 5.1 `dotnet build OpenDockify.sln` → 0 warnings / 0 errors
- [ ] 5.2 Unit tests for `AiGuard`:
  - fabricated amount in LLM output stripped
  - fabricated 18-digit ID stripped
  - mandatory values restored byte-for-byte (uppercase amount + name)
  - unselected clause content absent
- [ ] 5.3 HTTP smoke tests against local mock/Ollama:
  - `Ai.Enabled=false` → 403, no external call (mock asserts no hit)
  - enabled → polish-clause returns text; polish-document preserves values
  - LLM down → original text + warning, log row with Success=false
  - usage logs written with redacted snippets; admin can list them
