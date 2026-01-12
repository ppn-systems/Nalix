using Nalix.Common.Diagnostics;
using Nalix.Logging.Internal.Webhook.Models;
using Nalix.Logging.Options;

namespace Nalix.Logging.Internal.Formatters;

/// <summary>
/// Formats log entries into visually rich Discord webhook payloads.
/// </summary>
internal sealed class WebhookFormatter
{
    private const System.Int32 MaxFieldValueLength = 1024;

    private readonly WebhookLogOptions _options;

    public WebhookFormatter(WebhookLogOptions options)
    {
        System.ArgumentNullException.ThrowIfNull(options);
        _options = options;
    }

    /// <summary>
    /// Formats log entries into a Discord webhook payload.
    /// </summary>
    public WebhookPayload Format(
        System.Collections.Generic.IReadOnlyList<LogEntry> entries)
    {
        WebhookPayload payload = new()
        {
            Username = _options.Username,
            AvatarUrl = _options.AvatarUrl,
            Embeds = new(entries.Count)
        };

        foreach (var entry in entries)
        {
            payload.Embeds.Add(BuildEmbed(entry));
        }

        return payload;
    }

    private WebhookEmbed BuildEmbed(LogEntry entry)
    {
        WebhookEmbed embed = new()
        {
            Title = $"{GetEmoji(entry.LogLevel)} {entry.LogLevel.ToString().ToUpperInvariant()}",
            Description = FormatMessage(entry.Message),
            Color = GetColor(entry.LogLevel),
            Timestamp = _options.IncludeTimestamp ? entry.TimeStamp : null,
            Fields = BuildFields(entry),
            Footer = new WebhookEmbedFooter
            {
                Text = "Nalix Logging"
            }
        };

        return embed;
    }

    private System.Collections.Generic.List<WebhookEmbedField> BuildFields(LogEntry entry)
    {
        var fields = new System.Collections.Generic.List<WebhookEmbedField>(4)
        {
            new()
            {
                Name = "Level",
                Value = entry.LogLevel.ToString(),
                Inline = true
            }
        };

        if (entry.EventId.Id != 0)
        {
            fields.Add(new()
            {
                Name = "Event ID",
                Value = entry.EventId.Id.ToString(),
                Inline = true
            });
        }

        if (_options.IncludeTimestamp)
        {
            fields.Add(new()
            {
                Name = "Time",
                Value = entry.TimeStamp.ToString("yyyy-MM-dd HH:mm:ss"),
                Inline = true
            });
        }

        if (entry.Exception is not null && _options.IncludeStackTrace)
        {
            fields.Add(new()
            {
                Name = "Exception",
                Value = FormatException(entry.Exception),
                Inline = false
            });
        }

        return fields;
    }

    private static System.String FormatMessage(System.String message) => System.String.IsNullOrWhiteSpace(message) ? "*<empty message>*" : $"**{message}**";

    private static System.String FormatException(System.Exception exception)
    {
        System.String text = exception.StackTrace ?? exception.ToString();

        if (text.Length > MaxFieldValueLength - 20)
        {
            text = text[..(MaxFieldValueLength - 20)] + "...";
        }

        return $"```csharp\n{text}\n```";
    }

    private static System.Int32 GetColor(LogLevel level) => level switch
    {
        LogLevel.Meta => 0x95a5a6,
        LogLevel.Trace => 0xbdc3c7,
        LogLevel.Debug => 0x3498db,
        LogLevel.Information => 0x2ecc71,
        LogLevel.Warning => 0xf1c40f,
        LogLevel.Error => 0xe74c3c,
        LogLevel.Critical => 0x8e44ad,
        _ => 0x7f8c8d
    };

    private static System.String GetEmoji(LogLevel level) => level switch
    {
        LogLevel.Meta => "📊",
        LogLevel.Trace => "🔍",
        LogLevel.Debug => "🐛",
        LogLevel.Information => "ℹ️",
        LogLevel.Warning => "⚠️",
        LogLevel.Error => "❌",
        LogLevel.Critical => "🔥",
        _ => "📝"
    };
}
