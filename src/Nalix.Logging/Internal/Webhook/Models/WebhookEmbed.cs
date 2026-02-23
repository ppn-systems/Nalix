// Copyright(c) 2025 PPN Corporation. All rights reserved. 

using Nalix.Logging.Internal.Formatters;

namespace Nalix.Logging.Internal.Webhook.Models;

/// <summary>
/// Represents a Discord embed object for rich message formatting.
/// </summary>
internal sealed class WebhookEmbed
{
    /// <summary>
    /// Gets or sets the embed title (max 256 characters).
    /// </summary>
    public System.String? Title { get; set; }

    /// <summary>
    /// Gets or sets the embed description (max 4096 characters).
    /// </summary>
    public System.String? Description { get; set; }

    /// <summary>
    /// Gets or sets the embed color as an integer (RGB).
    /// </summary>
    public System.Int32 Color { get; set; }

    /// <summary>
    /// Gets or sets the timestamp in ISO8601 format.
    /// </summary>
    public System.DateTime? Timestamp { get; set; }

    /// <summary>
    /// Gets or sets the footer information.
    /// </summary>
    public WebhookEmbedFooter? Footer { get; set; }

    /// <summary>
    /// Gets or sets the array of embed fields (max 25).
    /// </summary>
    public System.Collections.Generic.List<WebhookEmbedField>? Fields { get; set; }

    /// <summary>
    /// Serializes the embed to JSON. Omits null properties and empty collections.
    /// </summary>
    public System.String ToJson()
    {
        System.Text.StringBuilder sb = new();
        sb.Append('{');
        System.Boolean first = true;

        if (!System.String.IsNullOrEmpty(Title))
        {
            sb.Append("\"title\":");
            sb.Append(JsonFormatter.Quote(Title));
            first = false;
        }

        if (!System.String.IsNullOrEmpty(Description))
        {
            if (!first)
            {
                sb.Append(',');
            }

            sb.Append("\"description\":");
            sb.Append(JsonFormatter.Quote(Description));
            first = false;
        }

        // Always include color (default 0) to match existing model behavior.
        if (!first)
        {
            sb.Append(',');
        }

        sb.Append("\"color\":");
        sb.Append(Color.ToString(System.Globalization.CultureInfo.InvariantCulture));
        first = false;

        if (Timestamp.HasValue)
        {
            if (!first)
            {
                sb.Append(',');
            }

            sb.Append("\"timestamp\":");
            sb.Append(JsonFormatter.Quote(JsonFormatter.FormatDateTime(Timestamp.Value)));
            first = false;
        }

        if (Footer is not null)
        {
            if (!first)
            {
                sb.Append(',');
            }

            sb.Append("\"footer\":");
            sb.Append(Footer.ToJson());
            first = false;
        }

        if (Fields?.Count > 0)
        {
            if (!first)
            {
                sb.Append(',');
            }

            sb.Append("\"fields\":[");
            System.Boolean firstField = true;
            foreach (var f in Fields)
            {
                if (!firstField)
                {
                    sb.Append(',');
                }

                sb.Append(f.ToJson());
                firstField = false;
            }
            sb.Append(']');
        }

        sb.Append('}');
        return sb.ToString();
    }
}
