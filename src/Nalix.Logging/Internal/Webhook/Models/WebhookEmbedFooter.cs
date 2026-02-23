// Copyright(c) 2025 PPN Corporation. All rights reserved. 

using Nalix.Logging.Internal.Formatters;

namespace Nalix.Logging.Internal.Webhook.Models;

/// <summary>
/// Represents a footer in a Discord embed.
/// </summary>
internal sealed class WebhookEmbedFooter
{
    /// <summary>
    /// Gets or sets the footer text (max 2048 characters).
    /// </summary>
    public System.String Text { get; set; } = System.String.Empty;

    /// <summary>
    /// Gets or sets the footer icon URL.
    /// </summary>
    public System.String? IconUrl { get; set; }

    /// <summary>
    /// Serializes the footer to JSON. Omits null properties.
    /// </summary>
    public System.String ToJson()
    {
        System.Text.StringBuilder sb = new();
        sb.Append('{');

        System.Boolean first = true;

        if (!System.String.IsNullOrEmpty(Text))
        {
            if (!first)
            {
                sb.Append(',');
            }

            sb.Append("\"text\":");
            sb.Append(Json.Quote(Text));
            first = false;
        }

        if (!System.String.IsNullOrEmpty(IconUrl))
        {
            if (!first)
            {
                sb.Append(',');
            }

            sb.Append("\"icon_url\":");
            sb.Append(Json.Quote(IconUrl));
        }

        sb.Append('}');
        return sb.ToString();
    }
}
