using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using OpenDockify.AiAssist.Services;
using OpenDockify.Api;
using OpenDockify.Data;
using OpenDockify.Finance.Services;
using OpenDockify.SystemConfig.Services;
using OpenDockify.Templates.Models;
using OpenDockify.Templates.Services;
using Platform.Ai;
using Platform.Ai.Contracts;
using Platform.Ai.Testing;
using Platform.Caching.InMemory;
using Platform.Caching.Keys;
using Platform.Core.Time;
using Platform.Quota.Contracts;
using Platform.Quota.Stores;
using Xunit;

namespace OpenDockify.UnitTests;

public sealed class AiCachingTests
{
    private const string _definition = """
        {
          "fields": [
            { "name": "borrowerName", "label": "借款人姓名", "type": "string", "required": true },
            { "name": "amount", "label": "金额", "type": "currency", "required": true }
          ],
          "clauses": [
            { "id": "penalty", "title": "违约金", "text": "逾期应支付违约金。" }
          ]
        }
        """;

    private const string _body = "借款人{{borrowerName}}借款{{amount}}元。";

    [Fact]
    public void Pii_scrubber_redacts_id_and_phone_before_send()
    {
        const string input = "借款人张三（身份证号11010519491231002X，电话13800138000）确认。";

        var scrubbed = AiPiiScrubber.Scrub(input);

        Assert.DoesNotContain("11010519491231002X", scrubbed);
        Assert.DoesNotContain("13800138000", scrubbed);
        Assert.Contains("[ID]", scrubbed);
        Assert.Contains("[PHONE]", scrubbed);
        Assert.True(AiPiiScrubber.ContainsRawPii(input));
        Assert.False(AiPiiScrubber.ContainsRawPii(scrubbed));
    }

    [Fact]
    public async Task Polish_clause_sends_scrubbed_prompt_and_logs_no_raw_pii()
    {
        await using var harness = await AssistHarness.CreateAsync(scrub: true);
        const string draft = "借款人张三（身份证号11010519491231002X，电话13800138000）确认借款。";
        harness.Llm.Reply = user => user;

        var result = await harness.Service.PolishClauseAsync(harness.OwnerId, harness.TemplateId, draft);

        Assert.Equal(AiAssistStatus.Ok, result.Status);
        Assert.NotNull(harness.Llm.LastUserContent);
        Assert.DoesNotContain("11010519491231002X", harness.Llm.LastUserContent);
        Assert.DoesNotContain("13800138000", harness.Llm.LastUserContent);
        Assert.DoesNotContain("11010519491231002X", result.Text);
        var stored = await harness.Db.Set<AiAssist.Models.AiUsageLog>().SingleAsync();
        Assert.DoesNotContain("11010519491231002X", stored.RequestSnippet);
        Assert.DoesNotContain("13800138000", stored.RequestSnippet);
        Assert.True(stored.RequestSnippet.Length <= 500);
    }

    [Fact]
    public async Task Legal_advice_output_carries_warning_banner_without_blocking()
    {
        await using var harness = await AssistHarness.CreateAsync(scrub: false);
        harness.Llm.Reply = _ => "建议你起诉对方，要求其承担法律责任。";

        var result = await harness.Service.PolishClauseAsync(harness.OwnerId, harness.TemplateId, "草稿文本。");

        Assert.Equal(AiAssistStatus.Ok, result.Status);
        Assert.Equal("建议你起诉对方，要求其承担法律责任。", result.Text);
        Assert.Contains(AiLegalAdviceClassifier.LegalAdviceWarning, result.Warning);
    }

    [Fact]
    public void Legal_advice_classifier_ignores_plain_polish()
    {
        Assert.False(AiLegalAdviceClassifier.LooksLikeLegalAdvice("借款人应按期还款。"));
        Assert.True(AiLegalAdviceClassifier.LooksLikeLegalAdvice("You should sue for damages."));
    }

    [Fact]
    public async Task Token_budget_exceeded_rejects_without_provider_call()
    {
        var quotas = new InMemoryQuotaStore(new SystemClock());
        var budgets = new AiTokenBudget(quotas, new FakeConfig(new() { [SettingKeys.AiMaxTokensPerDay] = 10 }));
        var userId = Guid.NewGuid();

        var (allowed, _, _) = await budgets.CheckAsync(userId, 5, default);
        Assert.True(allowed);
        await budgets.ConsumeAsync(userId, 8, default);
        var denied = await budgets.CheckAsync(userId, 5, default);
        Assert.False(denied.Allowed);

        var inner = new RecordingAiProvider(new FakeAiProvider());
        var client = new AiClient(
            new SingleAiProviderRouter(inner),
            new AiTokenBudgetPolicy(budgets));
        var request = new AiTextRequest(
            AiFeatureKey.Create("polish"),
            [new AiMessage("user", "hello")],
            "fake-model",
            metadata: new Dictionary<string, string>
            {
                [AiTokenBudgetPolicy.UserMetadataKey] = userId.ToString("N"),
                [AiTokenBudgetPolicy.EstimatedTokensMetadataKey] = "5",
            });
        var outcome = await client.GenerateAsync(request);

        Assert.False(outcome.Succeeded);
        Assert.Equal(AiFailureCategory.QuotaExceeded, outcome.Failure!.Category);
        Assert.Equal(0, inner.GenerationCalls);
    }

    [Fact]
    public async Task Insecure_remote_endpoint_fails_closed_without_http_call()
    {
        var handler = new StubHandler();
        var gateway = Gateway(
            handler,
            new() { [SettingKeys.AiProvider] = "openai-compatible", [SettingKeys.AiEndpoint] = "http://192.168.1.10:8080/v1", [SettingKeys.AiModel] = "m" },
            allowInsecureHttp: false);

        var result = await gateway.CompleteAsync("sys", "user", default);

        Assert.False(result.Succeeded);
        Assert.Contains("https://", result.Error);
        Assert.Equal(0, handler.Calls);
    }

    [Fact]
    public async Task Unknown_provider_fails_closed_without_http_call()
    {
        var handler = new StubHandler();
        var gateway = Gateway(
            handler,
            new() { [SettingKeys.AiProvider] = "anthropic", [SettingKeys.AiModel] = "m" },
            allowInsecureHttp: false);

        var result = await gateway.CompleteAsync("sys", "user", default);

        Assert.False(result.Succeeded);
        Assert.Equal(0, handler.Calls);
    }

    [Fact]
    public async Task Identical_polish_requests_hit_cache_without_second_provider_call()
    {
        var handler = new StubHandler
        {
            Responder = _ => JsonResponse(new { message = new { content = "polished-x" } }),
        };
        var gateway = Gateway(
            handler,
            new() { [SettingKeys.AiProvider] = "ollama", [SettingKeys.AiModel] = "llama3.1" },
            allowInsecureHttp: false);

        var first = await gateway.CompleteAsync("system-prompt", "user-text", default);
        var second = await gateway.CompleteAsync("system-prompt", "user-text", default);

        Assert.True(first.Succeeded);
        Assert.True(second.Succeeded);
        Assert.Equal("polished-x", first.Content);
        Assert.Equal("polished-x", second.Content);
        Assert.Equal(1, handler.Calls);
    }

    [Fact]
    public async Task Render_cache_key_stable_and_free_of_raw_pii()
    {
        var cache = new InMemoryCacheStore(new SystemClock());
        var service = new RenderCacheService(
            cache,
            new CacheKeyBuilder("opendockify"),
            new FakeConfig(new() { [SettingKeys.CacheRenderTtlMinutes] = 10 }),
            NullLogger<RenderCacheService>.Instance);
        var valuesA = new Dictionary<string, string> { ["amount"] = "12000", ["borrower"] = "张三13800138000" };
        var valuesB = new Dictionary<string, string> { ["borrower"] = "张三13800138000", ["amount"] = "12000" };
        var user = Guid.NewGuid();
        var template = Guid.NewGuid();
        var normA = RenderCacheService.NormalizeValues(user, template, "{}", "body", valuesA, ["c1"]);
        var normB = RenderCacheService.NormalizeValues(user, template, "{}", "body", valuesB, ["c1"]);
        Assert.Equal(normA, normB);

        var key = $"opendockify:a:render:{RenderCacheService.Sha256Hex(normA)}";
        Assert.DoesNotContain("13800138000", key);
        Assert.DoesNotContain(" ", key);

        var factoryCalls = 0;
        Task<string?> factory(CancellationToken ct)
        {
            factoryCalls++;
            return Task.FromResult<string?>("rendered-text");
        }

        var first = await service.GetOrCreateRenderAsync(normA, factory, default);
        var second = await service.GetOrCreateRenderAsync(normB, factory, default);

        Assert.False(first.Hit);
        Assert.True(second.Hit);
        Assert.Equal("rendered-text", second.Text);
        Assert.Equal(1, factoryCalls);
    }

    [Fact]
    public async Task Lpr_rate_cached_until_tag_invalidation()
    {
        var rates = new List<decimal> { 3.45m };
        var mutable = new MutableConfig(rates, 24);
        var cache = new InMemoryCacheStore(new SystemClock());
        var service = new InterestRateService(mutable, cache, new CacheKeyBuilder("opendockify"));

        var first = await service.ValidateAsync(4.0m);
        rates[0] = 9.99m;
        var cached = await service.ValidateAsync(4.0m);
        Assert.Equal(first.Message, cached.Message);
        Assert.Equal(first.Level, cached.Level);

        await cache.RemoveByTagAsync("lpr");
        var refreshed = await service.ValidateAsync(4.0m);
        Assert.NotEqual(first.Message, refreshed.Message);
    }

    [Fact]
    public void Golden_loan_and_lease_renders_are_deterministic()
    {
        const string definition = """
            {
              "fields": [
                { "name": "borrower", "label": "借款人", "type": "string", "required": true },
                { "name": "amount", "label": "金额", "type": "currency", "required": true },
                { "name": "signDate", "label": "签署日期", "type": "date", "required": true }
              ],
              "clauses": [
                { "id": "lease-term", "title": "租赁期限", "text": "租赁期自{{signDate}}起算。" }
              ]
            }
            """;
        const string loanBody = "借款人{{borrower}}借款{{amount}}元，签署于{{signDate}}。";
        const string leaseBody = "承租人{{borrower}}租金{{amount}}元，起租{{signDate}}。";
        var values = new Dictionary<string, string>
        {
            ["borrower"] = "李四",
            ["amount"] = "12000",
            ["signDate"] = "2026-09-01",
        };

        var loanValidation = TemplateDefinitionValidator.Validate(definition, loanBody);
        var leaseValidation = TemplateDefinitionValidator.Validate(definition, leaseBody);
        Assert.True(loanValidation.IsValid, loanValidation.Error);
        Assert.True(leaseValidation.IsValid, leaseValidation.Error);

        var loanOnce = TemplateRenderer.Render(loanValidation.Definition!, loanBody, values, []);
        var loanTwice = TemplateRenderer.Render(loanValidation.Definition!, loanBody, values, []);
        var leaseOnce = TemplateRenderer.Render(leaseValidation.Definition!, leaseBody, values, ["lease-term"]);
        var leaseTwice = TemplateRenderer.Render(leaseValidation.Definition!, leaseBody, values, ["lease-term"]);

        Assert.Equal(loanOnce.Text, loanTwice.Text);
        Assert.Equal(leaseOnce.Text, leaseTwice.Text);
        Assert.Contains("壹万贰仟", loanOnce.Text);
        Assert.Contains("2026年9月1日", loanOnce.Text);
        Assert.Contains("租赁期自2026年9月1日", leaseOnce.Text);
    }

    private static PlatformGatewayLlmClient Gateway(
        StubHandler handler,
        Dictionary<string, object?> settings,
        bool allowInsecureHttp)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Ai:AllowInsecureHttp"] = allowInsecureHttp ? "true" : "false" })
            .Build();
        var budgets = new AiTokenBudget(
            new InMemoryQuotaStore(new SystemClock()),
            new FakeConfig(new() { [SettingKeys.AiMaxTokensPerDay] = 0 }));
        return new PlatformGatewayLlmClient(
            new FakeConfig(settings),
            configuration,
            new StubFactory(handler),
            new InMemoryCacheStore(new SystemClock()),
            new CacheKeyBuilder("opendockify"),
            budgets,
            new HttpContextAccessor(),
            NullLogger<PlatformGatewayLlmClient>.Instance);
    }

    private static HttpResponseMessage JsonResponse<T>(T payload)
    {
        var json = JsonSerializer.Serialize(payload);
        return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json) };
    }

    private sealed class FakeConfig(Dictionary<string, object?> values) : ISystemConfigReader
    {
        public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
        {
            if (values.TryGetValue(key, out var value) && value is T typed)
            {
                return Task.FromResult<T?>(typed);
            }

            return Task.FromResult(default(T));
        }
    }

    private sealed class MutableConfig(List<decimal> rates, int ttlHours) : ISystemConfigReader
    {
        public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
        {
            object? value = key switch
            {
                SettingKeys.LprOneYearRate => rates[0],
                SettingKeys.CacheLprTtlHours => ttlHours,
                _ => null,
            };
            if (value is T typed)
            {
                return Task.FromResult<T?>(typed);
            }

            return Task.FromResult(default(T));
        }
    }

    private sealed class RecordingLlm : ILlmClient
    {
        public string? LastUserContent { get; private set; }

        public Func<string, string>? Reply { get; set; }

        public Task<LlmResult> CompleteAsync(string systemPrompt, string userContent, CancellationToken cancellationToken)
        {
            LastUserContent = userContent;
            return Task.FromResult(LlmResult.Success(Reply?.Invoke(userContent) ?? userContent));
        }
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        public int Calls { get; private set; }

        public Func<HttpRequestMessage, HttpResponseMessage> Responder { get; set; } =
            _ => new HttpResponseMessage(HttpStatusCode.OK);

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(Responder(request));
        }
    }

    private sealed class StubFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name)
        {
            return new HttpClient(handler, disposeHandler: false);
        }
    }

    private sealed class AssistHarness : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;

        private AssistHarness(SqliteConnection connection, AppDbContext db, AiAssistService service, RecordingLlm llm, Guid ownerId, Guid templateId)
        {
            _connection = connection;
            Db = db;
            Service = service;
            Llm = llm;
            OwnerId = ownerId;
            TemplateId = templateId;
        }

        public AppDbContext Db { get; }

        public AiAssistService Service { get; }

        public RecordingLlm Llm { get; }

        public Guid OwnerId { get; }

        public Guid TemplateId { get; }

        public static async Task<AssistHarness> CreateAsync(bool scrub)
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);
            await db.Database.EnsureCreatedAsync();
            var ownerId = Guid.NewGuid();
            var template = new Template
            {
                Id = Guid.NewGuid(),
                OwnerId = ownerId,
                Name = "Loan",
                Category = "legal",
                Body = _body,
                DefinitionJson = _definition,
            };
            db.Add(template);
            await db.SaveChangesAsync();
            var config = new FakeConfig(new()
            {
                [SettingKeys.AiEnabled] = true,
                [SettingKeys.AiScrubPii] = scrub,
                [SettingKeys.AiRateLimitPerDay] = 0,
            });
            var llm = new RecordingLlm();
            var service = new AiAssistService(config, new TemplateService(db), llm, new AiUsageLogService(db));
            return new(connection, db, service, llm, ownerId, template.Id);
        }

        public async ValueTask DisposeAsync()
        {
            await Db.DisposeAsync();
            await _connection.DisposeAsync();
        }
    }
}
