using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Platform.Mailing;

namespace OpenDockify.Api;

/// <summary>
/// Self-hosted notification options. Disabled by default; when disabled no
/// SMTP call is made and share/finalize flows still succeed.
/// </summary>
public sealed class NotifyOptions
{
    public const string SectionName = "Notifications";
    public bool Enabled { get; set; }
    public string FromAddress { get; set; } = "noreply@opendockify.local";
    public string FromDisplayName { get; set; } = "OpenDockify";
}

/// <summary>
/// Dev fallback <see cref="IMailService"/>: logs to server log (to + subject
/// only, no body/PII beyond what the intent needs) and reports Sent.
/// </summary>
public sealed partial class DevLogMailService(ILogger<DevLogMailService> logger, IOptions<NotifyOptions> options) : IMailService
{
    public Task<MailSendResult> SendAsync(MailMessage message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);
        var to = string.Join(",", message.To.Select(x => x.Address));
        Log.Fallback(logger, to, message.Subject, options.Value.FromAddress);
        return Task.FromResult(new MailSendResult(MailSendOutcome.Sent));
    }

    private static partial class Log
    {
        [LoggerMessage(1, LogLevel.Information, "Dev mail fallback to={To} subject={Subject} from={From}.")]
        public static partial void Fallback(ILogger logger, string to, string subject, string from);
    }
}

/// <summary>
/// Plaintext share/expiry/finalize intents over <see cref="IMailService"/>.
/// Disabled by default (safe dev fallback logs only when enabled with no SMTP).
/// </summary>
public sealed partial class NotifyService(IMailService mail, IOptions<NotifyOptions> options, ILogger<NotifyService> logger)
{
    public async Task<bool> SendShareGrantedAsync(string recipient, string docTitle, string linkUrl, CancellationToken ct)
    {
        if (!options.Value.Enabled)
        {
            return false;
        }

        if (!IsEmail(recipient))
        {
            Log.Skipped(logger);
            return false;
        }

        var msg = new MailMessage(
            new MailAddress(options.Value.FromAddress, options.Value.FromDisplayName),
            [new MailAddress(recipient.Trim())],
            $"Shared document: {docTitle}",
            $"You were granted access to \"{docTitle}\".\nOpen: {linkUrl}\n");
        var result = await mail.SendAsync(msg, ct);
        Log.Sent(logger, result.Outcome);
        return result.Outcome == MailSendOutcome.Sent;
    }

    public async Task<bool> SendLinkExpiringAsync(string recipient, string docTitle, DateTimeOffset expiresAt, CancellationToken ct)
    {
        if (!options.Value.Enabled)
        {
            return false;
        }

        if (!IsEmail(recipient))
        {
            return false;
        }

        var msg = new MailMessage(
            new MailAddress(options.Value.FromAddress, options.Value.FromDisplayName),
            [new MailAddress(recipient.Trim())],
            $"Share expiring: {docTitle}",
            $"The share for \"{docTitle}\" expires at {expiresAt:u}.\n");
        var result = await mail.SendAsync(msg, ct);
        return result.Outcome == MailSendOutcome.Sent;
    }

    public async Task<bool> SendDocumentFinalizedAsync(string recipient, string docTitle, Guid documentId, CancellationToken ct)
    {
        if (!options.Value.Enabled)
        {
            return false;
        }

        if (!IsEmail(recipient))
        {
            return false;
        }

        var msg = new MailMessage(
            new MailAddress(options.Value.FromAddress, options.Value.FromDisplayName),
            [new MailAddress(recipient.Trim())],
            $"Document finalized: {docTitle}",
            $"Document \"{docTitle}\" finalized (id {documentId}).\n");
        var result = await mail.SendAsync(msg, ct);
        return result.Outcome == MailSendOutcome.Sent;
    }

    internal static bool IsEmail(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var at = value.IndexOf('@');
        return at > 0 && at < value.Length - 1 && value.Contains('.', StringComparison.Ordinal);
    }

    private static partial class Log
    {
        [LoggerMessage(1, LogLevel.Information, "Share notification skipped for non-email recipient.")]
        public static partial void Skipped(ILogger logger);

        [LoggerMessage(2, LogLevel.Information, "Share notification outcome={Outcome}.")]
        public static partial void Sent(ILogger logger, MailSendOutcome outcome);
    }
}

public static class NotifyModuleExtensions
{
    public static IServiceCollection AddNotifyModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<NotifyOptions>(configuration.GetSection(NotifyOptions.SectionName));
        services.Configure<Platform.Mailing.MailingOptions>(configuration.GetSection(Platform.Mailing.MailingOptions.SectionName));
        services.Configure<Platform.Notifications.NotificationOptions>(o =>
        {
            configuration.GetSection(Platform.Notifications.NotificationOptions.SectionName).Bind(o);
            o.MaxAttempts = Math.Clamp(o.MaxAttempts, 1, 10);
        });

        var smtpHost = configuration["Mailing:Smtp:Host"];
        if (!string.IsNullOrWhiteSpace(smtpHost))
        {
            services.AddOptions<Platform.Mailing.Smtp.SmtpMailOptions>()
                .Configure(o => configuration.GetSection(Platform.Mailing.Smtp.SmtpMailOptions.SectionName).Bind(o));
            services.AddSingleton<IMailService, Platform.Mailing.Smtp.SmtpMailService>();
        }
        else
        {
            services.AddSingleton<IMailService, DevLogMailService>();
        }

        services.AddSingleton<NotifyService>();
        return services;
    }
}
