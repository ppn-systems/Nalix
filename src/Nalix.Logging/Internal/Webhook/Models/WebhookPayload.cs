// Copyright(c) 2025 PPN Corporation. All rights reserved. 

namespace Nalix.Logging.Internal.Webhook.Models;

/// <summary>
/// Represents the root payload structure for Discord webhook requests.
/// </summary>
/// <remarks>
/// See Discord webhook documentation:  https://discord.com/developers/docs/resources/webhook
/// </remarks>
internal sealed class WebhookPayload
{
    /// <summary>
    /// Gets or sets the message content (up to 2000 characters).
    /// </summary>
    [System.Text.Json.Serialization.JsonPropertyName("content")]
    public System.String? Content { get; set; }

    /// <summary>
    /// Gets or sets the username override for the webhook.
    /// </summary>
    [System.Text.Json.Serialization.JsonPropertyName("username")]
    public System.String? Username { get; set; }

    /// <summary>
    /// Gets or sets the avatar URL override for the webhook.
    /// </summary>
    [System.Text.Json.Serialization.JsonPropertyName("avatar_url")]
    public System.String? AvatarUrl { get; set; }

    /// <summary>
    /// Gets or sets the array of embeds (max 10 per message).
    /// </summary>
    [System.Text.Json.Serialization.JsonPropertyName("embeds")]
    public System.Collections.Generic.List<WebhookEmbed>? Embeds { get; set; }
}