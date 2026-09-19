## 1. Gateway and policy

- [x] 1.1 Add pinned refs `Platform.Ai`, `Platform.Ai.Contracts`, `Platform.Ai.Ollama`, `Platform.Ai.OpenAiCompatible`, `Platform.Caching`, `Platform.Caching.Hybrid`
- [x] 1.2 Wrap `AiAssistService` with `IGenerationGateway` routing (Ollama default, HTTPS-only remote) + token budget via quota store

## 2. Scrub and cache

- [x] 2.1 Add PII scrub (default on) + `AiGuard` re-substitution + 500-char redacted logs + legal-advice flag
- [x] 2.2 Add `HybridCacheStore` for renders + LPR (SHA-256 keys, 10-min/24-h TTL, invalidate endpoint)

## 3. Verify

- [x] 3.1 `dotnet build` 0/0; PII redacted pre-send, budget 429 no-call, insecure-remote fail-closed
- [x] 3.2 Golden loan/lease renders, cache hit telemetry safe; `openspec validate --change platform-ai-caching --strict`
