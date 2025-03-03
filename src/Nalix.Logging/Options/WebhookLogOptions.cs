// Copyright (c) 2025 PPN Corporation.  All rights reserved.

namespace Nalix.Logging.Options;

/// <summary>
/// Configuration options for the Discord webhook logger.
/// </summary>
[System.Diagnostics.DebuggerDisplay("WebhookUrl={_webhookUrl,nq}, BatchSize={BatchSize}")]
public sealed class WebhookLogOptions
{
    #region Constants

    private const System.Int32 MinBatchSize = 1;
    private const System.Int32 MaxBatchSize = 10; // Discord rate limit consideration
    private const System.Int32 DefaultBatchSize = 10;
    private const System.Int32 DefaultRetryCount = 3;
    private const System.Int32 DefaultMaxQueueSize = 1000;

    #endregion Constants

    #region Fields

    private static readonly System.TimeSpan DefaultRetryDelay = System.TimeSpan.FromSeconds(1);
    private static readonly System.TimeSpan DefaultBatchDelay = System.TimeSpan.FromSeconds(2);
    private static readonly System.TimeSpan DefaultHttpTimeout = System.TimeSpan.FromSeconds(30);

    private System.String? _webhookUrl;
    private System.Int32 _batchSize = DefaultBatchSize;
    private System.Int32 _retryCount = DefaultRetryCount;
    private System.Int32 _maxQueueSize = DefaultMaxQueueSize;

    #endregion Fields

    #region Properties

    /// <summary>
    /// Gets or sets the Discord webhook URL.
    /// </summary>
    /// <remarks>
    /// This is required.  You can create a webhook in Discord server settings under Integrations.
    /// Format: https://discord.com/api/webhooks/{webhook. id}/{webhook.token}
    /// </remarks>
    /// <exception cref="System.ArgumentException">Thrown when value is null, empty, or not a valid URL.</exception>
    public System.String WebhookUrl
    {
        get => _webhookUrl ?? throw new System.InvalidOperationException("WebhookUrl must be set before use.");
        set
        {
            if (System.String.IsNullOrWhiteSpace(value))
            {
                throw new System.ArgumentException("WebhookUrl cannot be null or empty.", nameof(value));
            }

            if (!System.Uri.TryCreate(value, System.UriKind.Absolute, out var uri) ||
                (!uri.Scheme.Equals("http", System.StringComparison.OrdinalIgnoreCase) &&
                 !uri.Scheme.Equals("https", System.StringComparison.OrdinalIgnoreCase)))
            {
                throw new System.ArgumentException("WebhookUrl must be a valid HTTP or HTTPS URL.", nameof(value));
            }

            _webhookUrl = value;
        }
    }

    /// <summary>
    /// Gets or sets the username displayed for log messages in Discord.
    /// </summary>
    /// <remarks>
    /// If not set, Discord will use the webhook's default username.
    /// </remarks>
    public System.String? Username { get; set; } = "Nalix Logger";

    /// <summary>
    /// Gets or sets the avatar URL displayed for log messages in Discord.
    /// </summary>
    /// <remarks>
    /// If not set, Discord will use the webhook's default avatar.
    /// </remarks>
    public System.String? AvatarUrl { get; set; }

    /// <summary>
    /// Gets or sets the maximum number of log entries to batch before sending to Discord.
    /// </summary>
    /// <remarks>
    /// Discord has rate limits, so batching helps prevent hitting those limits.
    /// Maximum value is 10 to stay within Discord's embed limits per message.
    /// </remarks>
    /// <exception cref="System.ArgumentOutOfRangeException">Thrown when value is less than 1 or greater than 10.</exception>
    public System.Int32 BatchSize
    {
        get => _batchSize;
        set
        {
            if (value is < MinBatchSize or > MaxBatchSize)
            {
                throw new System.ArgumentOutOfRangeException(
                    nameof(value), $"BatchSize must be between {MinBatchSize} and {MaxBatchSize}.");
            }

            _batchSize = value;
        }
    }

    /// <summary>
    /// Gets or sets the maximum number of log entries that can be queued.
    /// </summary>
    /// <remarks>
    /// When the queue is full, behavior depends on <see cref="BlockWhenQueueFull"/>.
    /// </remarks>
    /// <exception cref="System.ArgumentOutOfRangeException">Thrown when value is less than 1.</exception>
    public System.Int32 MaxQueueSize
    {
        get => _maxQueueSize;
        set
        {
            if (value < 1)
            {
                throw new System.ArgumentOutOfRangeException(
                    nameof(value), "MaxQueueSize must be at least 1.");
            }

            _maxQueueSize = value;
        }
    }

    /// <summary>
    /// Gets or sets the delay between batch sends.
    /// </summary>
    /// <remarks>
    /// Logs will be sent either when <see cref="BatchSize"/> is reached or this delay elapses.
    /// </remarks>
    public System.TimeSpan BatchDelay { get; set; } = DefaultBatchDelay;

    /// <summary>
    /// Gets or sets the HTTP request timeout for webhook calls.
    /// </summary>
    public System.TimeSpan HttpTimeout { get; set; } = DefaultHttpTimeout;

    /// <summary>
    /// Gets or sets the number of retry attempts for failed webhook calls.
    /// </summary>
    /// <exception cref="System.ArgumentOutOfRangeException">Thrown when value is less than 0.</exception>
    public System.Int32 RetryCount
    {
        get => _retryCount;
        set
        {
            if (value < 0)
            {
                throw new System.ArgumentOutOfRangeException(
                    nameof(value), "RetryCount cannot be negative.");
            }

            _retryCount = value;
        }
    }

    /// <summary>
    /// Gets or sets the delay between retry attempts.
    /// </summary>
    public System.TimeSpan RetryDelay { get; set; } = DefaultRetryDelay;

    /// <summary>
    /// Gets or sets a value indicating whether to use Discord embeds for log formatting.
    /// </summary>
    /// <remarks>
    /// When true, logs are sent as rich embeds with colors and fields.
    /// When false, logs are sent as plain text messages.
    /// </remarks>
    public System.Boolean UseEmbeds { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether to block when the queue is full.
    /// </summary>
    /// <remarks>
    /// When true, logging will block until queue space is available.
    /// When false, log entries will be discarded when the queue is full.
    /// </remarks>
    public System.Boolean BlockWhenQueueFull { get; set; } = false;

    /// <summary>
    /// Gets or sets the minimum log level to send to Discord.
    /// </summary>
    /// <remarks>
    /// Logs below this level will be filtered out.  This helps reduce noise in Discord.
    /// Default is Warning to avoid spamming Discord with debug/info messages.
    /// </remarks>
    public Nalix.Common.Logging.LogLevel MinimumLevel { get; set; } = Nalix.Common.Logging.LogLevel.Warning;

    /// <summary>
    /// Gets or sets a value indicating whether to include stack traces in error logs.
    /// </summary>
    public System.Boolean IncludeStackTrace { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether to include timestamp in log messages.
    /// </summary>
    public System.Boolean IncludeTimestamp { get; set; } = true;

    /// <summary>
    /// Gets or sets a custom error handler for webhook failures.
    /// </summary>
    /// <remarks>
    /// This handler will be called when all retry attempts have failed.
    /// Use this to implement fallback logging or alerting mechanisms.
    /// </remarks>
    public System.Action<System.Exception, System.String>? OnWebhookError { get; set; }

    #endregion Properties
}