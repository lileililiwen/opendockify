namespace OpenDockify.Integrations.Configuration;

public sealed class IntegrationsOptions
{
    public const string SectionName = "Integrations";

    public WebhookOptions Webhooks { get; set; } = new();

    /// <summary>How long completed idempotency records are retained and replayable.</summary>
    public int IdempotencyRetentionDays { get; set; } = 30;

    /// <summary>Maximum service tokens per owner (abuse bound).</summary>
    public int MaxTokensPerOwner { get; set; } = 20;

    /// <summary>Maximum webhook subscriptions per owner.</summary>
    public int MaxSubscriptionsPerOwner { get; set; } = 10;

    public int GetIdempotencyRetentionDays()
    {
        return Clamp(IdempotencyRetentionDays, 1, 365, 30);
    }

    public int GetMaxTokensPerOwner()
    {
        return Clamp(MaxTokensPerOwner, 1, 1000, 20);
    }

    public int GetMaxSubscriptionsPerOwner()
    {
        return Clamp(MaxSubscriptionsPerOwner, 1, 100, 10);
    }

    private static int Clamp(int value, int min, int max, int fallback)
    {
        return value >= min && value <= max ? value : fallback;
    }
}

/// <summary>
/// Outbound webhook delivery controls. Delivery is disabled by default so a
/// fresh deployment makes no outbound requests until the deployer opts in.
/// </summary>
public sealed class WebhookOptions
{
    /// <summary>Master switch for outbound webhook delivery (default: off).</summary>
    public bool Enabled { get; set; }

    /// <summary>Bounded retry budget per delivery.</summary>
    public int MaxAttempts { get; set; } = 8;

    /// <summary>Base delay for exponential backoff (delay = base * 2^(attempt-1), capped at 1 hour).</summary>
    public int BaseRetryDelaySeconds { get; set; } = 30;

    /// <summary>Per-attempt HTTP timeout.</summary>
    public int TimeoutSeconds { get; set; } = 15;

    /// <summary>Worker lease duration; expired leases are reclaimed after a crash.</summary>
    public int LeaseMinutes { get; set; } = 5;

    /// <summary>How long terminal delivery records stay inspectable.</summary>
    public int RetentionDays { get; set; } = 30;

    /// <summary>Worker poll interval.</summary>
    public double PollIntervalSeconds { get; set; } = 5;

    /// <summary>Maximum redirects followed per attempt (each hop re-validated).</summary>
    public int MaxRedirects { get; set; } = 3;

    /// <summary>
    /// Deployer allowlist of hostnames or CIDR ranges (e.g. "10.0.0.0/8",
    /// "metrics.internal") that are exempt from prohibited-address blocking.
    /// </summary>
    public List<string> Allowlist { get; set; } = [];

    public int GetMaxAttempts()
    {
        return Clamp(MaxAttempts, 1, 20, 8);
    }

    public int GetBaseRetryDelaySeconds()
    {
        return Clamp(BaseRetryDelaySeconds, 1, 3600, 30);
    }

    public int GetTimeoutSeconds()
    {
        return Clamp(TimeoutSeconds, 1, 120, 15);
    }

    public int GetLeaseMinutes()
    {
        return Clamp(LeaseMinutes, 1, 60, 5);
    }

    public int GetRetentionDays()
    {
        return Clamp(RetentionDays, 1, 365, 30);
    }

    public TimeSpan GetPollInterval()
    {
        return TimeSpan.FromSeconds(Clamp((int)PollIntervalSeconds, 1, 600, 5));
    }

    public int GetMaxRedirects()
    {
        return Clamp(MaxRedirects, 0, 10, 3);
    }

    private static int Clamp(int value, int min, int max, int fallback)
    {
        return value >= min && value <= max ? value : fallback;
    }
}
