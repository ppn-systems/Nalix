// Copyright(c) 2025 PPN Corporation. All rights reserved. 

namespace Nalix.Logging.Internal.Webhook.Models;

/// <summary>
/// Represents a Discord embed object for rich message formatting.
/// </summary>
internal sealed class WebhookEmbed
{
    /// <summary>
    /// Gets or sets the embed title (max 256 characters).
    /// </summary>
    [System.Text.Json.Serialization.JsonPropertyName("title")]
    public System.String? Title { get; set; }

    /// <summary>
    /// Gets or sets the embed description (max 4096 characters).
    /// </summary>
    [System.Text.Json.Serialization.JsonPropertyName("description")]
    public System.String? Description { get; set; }

    /// <summary>
    /// Gets or sets the embed color as an integer (RGB).
    /// </summary>
    [System.Text.Json.Serialization.JsonPropertyName("color")]
    public System.Int32 Color { get; set; }

    /// <summary>
    /// Gets or sets the timestamp in ISO8601 format.
    /// </summary>
    [System.Text.Json.Serialization.JsonPropertyName("timestamp")]
    public System.DateTime? Timestamp { get; set; }

    /// <summary>
    /// Gets or sets the footer information. 
    /// </summary>
    [System.Text.Json.Serialization.JsonPropertyName("footer")]
    public WebhookEmbedFooter? Footer { get; set; }

    /// <summary>
    /// Gets or sets the array of embed fields (max 25).
    /// </summary>
    [System.Text.Json.Serialization.JsonPropertyName("fields")]
    public System.Collections.Generic.List<WebhookEmbedField>? Fields { get; set; }
}
