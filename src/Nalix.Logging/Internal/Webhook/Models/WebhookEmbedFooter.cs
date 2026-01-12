// Copyright(c) 2025 PPN Corporation. All rights reserved. 

namespace Nalix.Logging.Internal.Webhook.Models;

/// <summary>
/// Represents a footer in a Discord embed.
/// </summary>
internal sealed class WebhookEmbedFooter
{
    /// <summary>
    /// Gets or sets the footer text (max 2048 characters).
    /// </summary>
    [System.Text.Json.Serialization.JsonPropertyName("text")]
    public System.String Text { get; set; } = System.String.Empty;

    /// <summary>
    /// Gets or sets the footer icon URL. 
    /// </summary>
    [System.Text.Json.Serialization.JsonPropertyName("icon_url")]
    public System.String? IconUrl { get; set; }
}
