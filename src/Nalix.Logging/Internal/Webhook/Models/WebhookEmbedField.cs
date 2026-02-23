// Copyright(c) 2025 PPN Corporation. All rights reserved. 

using Nalix.Logging.Internal.Formatters;

namespace Nalix.Logging.Internal.Webhook.Models;

/// <summary>
/// Represents a field in a Discord embed.
/// </summary>
internal sealed class WebhookEmbedField
{
    /// <summary>
    /// Gets or sets the field name (max 256 characters).
    /// </summary>
    public System.String Name { get; set; } = System.String.Empty;

    /// <summary>
    /// Gets or sets the field value (max 1024 characters).
    /// </summary>
    public System.String Value { get; set; } = System.String.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether the field should be displayed inline.
    /// </summary>
    public System.Boolean Inline { get; set; }

    /// <summary>
    /// Serializes the field to JSON. Omits null properties.
    /// </summary>
    public System.String ToJson()
    {
        System.Text.StringBuilder sb = new();
        sb.Append('{');

        System.Boolean first = true;

        if (!System.String.IsNullOrEmpty(Name))
        {
            if (!first)
            {
                sb.Append(',');
            }

            sb.Append("\"name\":");
            sb.Append(JsonFormatter.Quote(Name));
            first = false;
        }

        if (!System.String.IsNullOrEmpty(Value))
        {
            if (!first)
            {
                sb.Append(',');
            }

            sb.Append("\"value\":");
            sb.Append(JsonFormatter.Quote(Value));
            first = false;
        }

        if (Inline)
        {
            if (!first)
            {
                sb.Append(',');
            }

            sb.Append("\"inline\":true");
        }

        sb.Append('}');
        return sb.ToString();
    }
}
