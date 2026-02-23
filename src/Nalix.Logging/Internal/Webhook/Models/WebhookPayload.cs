// Copyright(c) 2025 PPN Corporation. All rights reserved. 

using Nalix.Logging.Internal.Formatters;

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
    public System.String? Content { get; set; }

    /// <summary>
    /// Gets or sets the username override for the webhook.
    /// </summary>
    public System.String? Username { get; set; }

    /// <summary>
    /// Gets or sets the avatar URL override for the webhook.
    /// </summary>
    public System.String? AvatarUrl { get; set; }

    /// <summary>
    /// Gets or sets the array of embeds (max 10 per message).
    /// </summary>
    public System.Collections.Generic.List<WebhookEmbed>? Embeds { get; set; }

    /// <summary>
    /// Serializes the payload to JSON. Omits nulls and empty arrays.
    /// </summary>
    public System.String ToJson()
    {
        System.Text.StringBuilder sb = new();
        sb.Append('{');

        System.Boolean first = true;

        if (!System.String.IsNullOrEmpty(Content))
        {
            sb.Append("\"content\":");
            sb.Append(Json.Quote(Content));
            first = false;
        }

        if (!System.String.IsNullOrEmpty(Username))
        {
            if (!first)
            {
                sb.Append(',');
            }

            sb.Append("\"username\":");
            sb.Append(Json.Quote(Username));
            first = false;
        }

        if (!System.String.IsNullOrEmpty(AvatarUrl))
        {
            if (!first)
            {
                sb.Append(',');
            }

            sb.Append("\"avatar_url\":");
            sb.Append(Json.Quote(AvatarUrl));
            first = false;
        }

        if (Embeds is not null && Embeds.Count > 0)
        {
            if (!first)
            {
                sb.Append(',');
            }

            sb.Append("\"embeds\":[");
            System.Boolean firstEmbed = true;
            foreach (var e in Embeds)
            {
                if (!firstEmbed)
                {
                    sb.Append(',');
                }

                sb.Append(e.ToJson());
                firstEmbed = false;
            }
            sb.Append(']');
            first = false;
        }

        sb.Append('}');
        return sb.ToString();
    }
}