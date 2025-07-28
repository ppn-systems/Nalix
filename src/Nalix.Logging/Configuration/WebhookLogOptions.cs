// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Nalix.Common.Diagnostics.Enums;
using Nalix.Common.Shared.Attributes;
using Nalix.Framework.Configuration.Binding;

namespace Nalix.Logging.Configuration;

/// <summary>
/// Configuration options for the Discord webhook logger.
/// </summary>
[System.Diagnostics.DebuggerDisplay("WebhookUrl={_webhookUrl,nq}, BatchSize={BatchSize}")]
[IniComment("Discord webhook logger configuration — controls batching, retries, formatting, and filtering")]
public sealed class WebhookLogOptions : ConfigurationLoader
{
    #region Constants

    private const System.Int32 MinBatchSize = 1;
    private const System.Int32 MaxBatchSize = 10;
    private const System.Int32 DefaultBatchSize = 10;
    private const System.Int32 DefaultRetryCount = 3;
    private const System.Int32 DefaultMaxQueueSize = 1000;

    #endregion Constants

    #region Fields

    private static readonly System.TimeSpan DefaultRetryDelay = System.TimeSpan.FromSeconds(1);
    private static readonly System.TimeSpan DefaultBatchDelay = System.TimeSpan.FromSeconds(2);
    private static readonly System.TimeSpan DefaultHttpTimeout = System.TimeSpan.FromSeconds(30);

    private System.Int32 _batchSize = DefaultBatchSize;
    private System.Int32 _retryCount = DefaultRetryCount;
    private System.Int32 _maxQueueSize = DefaultMaxQueueSize;
    private System.Collections.Generic.List<System.String> _webhookUrls = [];

    #endregion Fields

    #region Properties

    /// <summary>
    /// Gets or sets the Discord webhook URLs.
    /// </summary>
    [ConfiguredIgnore]
    public System.Collections.Generic.List<System.String> WebhookUrls
    {
        get => _webhookUrls;
        set
        {
            if (value is null || value.Count is 0)
            {
                throw new System.ArgumentException("WebhookUrls must contain at least one URL.", nameof(value));
            }

            foreach (var url in value)
            {
                if (System.String.IsNullOrWhiteSpace(url))
                {
                    throw new System.ArgumentException("WebhookUrl cannot be null or empty.", nameof(value));
                }

                if (!System.Uri.TryCreate(url, System.UriKind.Absolute, out var uri) ||
                    (!uri.Scheme.Equals("http", System.StringComparison.OrdinalIgnoreCase) &&
                     !uri.Scheme.Equals("https", System.StringComparison.OrdinalIgnoreCase)))
                {
                    throw new System.ArgumentException($"Invalid webhook URL: {url}", nameof(value));
                }
            }

            _webhookUrls = value;
        }
    }

    /// <summary>
    /// Gets or sets the load balancing strategy for multiple webhooks.
    /// </summary>
    [IniComment("Dispatch strategy when multiple webhook URLs are configured (e.g. RoundRobin, Broadcast)")]
    public WebhookDispatchMode DispatchMode { get; set; } = WebhookDispatchMode.RoundRobin;

    /// <summary>
    /// Gets or sets the username displayed for log messages in Discord.
    /// </summary>
    [IniComment("Display name shown in Discord for log messages (empty = webhook default)")]
    public System.String Username { get; set; } = "NLogx";

    /// <summary>
    /// Gets or sets the avatar URL displayed for log messages in Discord.
    /// </summary>
    [IniComment("Avatar image URL for the webhook bot (empty = webhook default)")]
    public System.String AvatarUrl { get; set; } = System.String.Empty;

    /// <summary>
    /// Gets or sets the maximum number of log entries to batch before sending to Discord.
    /// </summary>
    /// <exception cref="System.ArgumentOutOfRangeException">Thrown when value is less than 1 or greater than 10.</exception>
    [IniComment("Log entries to accumulate per Discord message (1–10, Discord embed limit)")]
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
    /// <exception cref="System.ArgumentOutOfRangeException">Thrown when value is less than 1.</exception>
    [IniComment("Maximum queued log entries before blocking or discarding (minimum 1)")]
    public System.Int32 MaxQueueSize
    {
        get => _maxQueueSize;
        set
        {
            if (value < 1)
            {
                throw new System.ArgumentOutOfRangeException(nameof(value), "MaxQueueSize must be at least 1.");
            }

            _maxQueueSize = value;
        }
    }

    /// <summary>
    /// Gets or sets the delay between batch sends.
    /// </summary>
    [IniComment("Max wait time before flushing a partial batch (e.g. 00:00:02 = 2 seconds)")]
    public System.TimeSpan BatchDelay { get; set; } = DefaultBatchDelay;

    /// <summary>
    /// Gets or sets the HTTP request timeout for webhook calls.
    /// </summary>
    [IniComment("HTTP request timeout per webhook call (e.g. 00:00:30 = 30 seconds)")]
    public System.TimeSpan HttpTimeout { get; set; } = DefaultHttpTimeout;

    /// <summary>
    /// Gets or sets the number of retry attempts for failed webhook calls.
    /// </summary>
    /// <exception cref="System.ArgumentOutOfRangeException">Thrown when value is less than 0.</exception>
    [IniComment("Number of retry attempts on webhook failure (0 = no retries)")]
    public System.Int32 RetryCount
    {
        get => _retryCount;
        set
        {
            if (value < 0)
            {
                throw new System.ArgumentOutOfRangeException(nameof(value), "RetryCount cannot be negative.");
            }

            _retryCount = value;
        }
    }

    /// <summary>
    /// Gets or sets the delay between retry attempts.
    /// </summary>
    [IniComment("Delay between retry attempts (e.g. 00:00:01 = 1 second)")]
    public System.TimeSpan RetryDelay { get; set; } = DefaultRetryDelay;

    /// <summary>
    /// Gets or sets a value indicating whether to use Discord embeds for log formatting.
    /// </summary>
    [IniComment("Send logs as rich Discord embeds with colors (false = plain text)")]
    public System.Boolean UseEmbeds { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether to block when the queue is full.
    /// </summary>
    [IniComment("Block the caller when the queue is full (false = discard entries instead)")]
    public System.Boolean BlockWhenQueueFull { get; set; } = false;

    /// <summary>
    /// Gets or sets the minimum log level to send to Discord.
    /// </summary>
    [IniComment("Minimum log level forwarded to Discord (e.g. Warning — suppresses Debug and Info noise)")]
    public LogLevel MinimumLevel { get; set; } = LogLevel.Warning;

    /// <summary>
    /// Gets or sets a value indicating whether to include stack traces in error logs.
    /// </summary>
    [IniComment("Attach stack traces to error and fatal log entries")]
    public System.Boolean IncludeStackTrace { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether to include timestamp in log messages.
    /// </summary>
    [IniComment("Include a timestamp in each Discord log message")]
    public System.Boolean IncludeTimestamp { get; set; } = true;

    /// <summary>
    /// Gets or sets a custom error handler for webhook failures.
    /// </summary>
    [ConfiguredIgnore]
    public System.Action<System.Exception, System.String>? OnWebhookError { get; set; }

    #endregion Properties
}