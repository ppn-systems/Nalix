// Copyright(c) 2025 PPN Corporation. All rights reserved. 

namespace Nalix.Logging.Internal.Webhook.Models;

/// <summary>
/// Represents a field in a Discord embed.
/// </summary>
internal sealed class WebhookEmbedField
{
    /// <summary>
    /// Gets or sets the field name (max 256 characters).
    /// </summary>
    [System.Text.Json.Serialization.JsonPropertyName("name")]
    public System.String Name { get; set; } = System.String.Empty;

    /// <summary>
    /// Gets or sets the field value (max 1024 characters).
    /// </summary>
    [System.Text.Json.Serialization.JsonPropertyName("value")]
    public System.String Value { get; set; } = System.String.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether the field should be displayed inline.
    /// </summary>
    [System.Text.Json.Serialization.JsonPropertyName("inline")]
    public System.Boolean Inline { get; set; }
}
