## 1. Pipeline

- [ ] 1.1 Add pinned refs `Platform.AspNetCore`, `Platform.Web`, `Platform.Web.Cors|Resilience|OpenApi|Versioning|Telemetry`; reorder `Program.cs` pipeline
- [ ] 1.2 Add CORS deny-default + `Web:Cors:AllowedOrigins`; add security headers + versioning + OpenApi registry

## 2. Guards

- [ ] 2.1 Add `ForwardedHeaders` trusted-proxy config; add `Sharing:HashKey` separation validator (fail closed)
- [ ] 2.2 Add AI HTTPS-only guard (`Ai:AllowInsecureHttp` loopback exception); wire resilient HttpClients

## 3. Verify

- [ ] 3.1 `dotnet build` 0/0; correlation echo, sanitized 500+`code`, CORS preflight deny/allow, forwarded-IP keying
- [ ] 3.2 Docs + Flutter error `code` handling; `openspec validate --change platform-web-edge --strict`
