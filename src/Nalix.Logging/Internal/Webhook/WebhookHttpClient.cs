// Copyright (c) 2025 PPN Corporation. All rights reserved. 

using Nalix.Common.Core.Enums;
using Nalix.Common.Diagnostics;
using Nalix.Logging.Internal.Formatters;
using Nalix.Logging.Internal.Webhook.Models;
using Nalix.Logging.Options;

namespace Nalix.Logging.Internal.Webhook;

/// <summary>
/// Handles HTTP communication with Discord webhook API.
/// Manages retry logic, rate limiting, and payload formatting.
/// </summary>
internal sealed class WebhookHttpClient : System.IDisposable
{
    #region Fields

    private readonly System.Random _random;
    private volatile System.Boolean _disposed;
    private readonly WebhookLogOptions _options;
    private readonly WebhookFormatter _formatter;
    private readonly System.Net.Http.HttpClient _httpClient;
    private readonly System.Text.Json.JsonSerializerOptions _jsonOptions;

    private System.Int32 _currentWebhookIndex;

    #endregion Fields

    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="WebhookHttpClient"/> class.
    /// </summary>
    /// <param name="options">The webhook log options.</param>
    public WebhookHttpClient(WebhookLogOptions options)
    {
        System.ArgumentNullException.ThrowIfNull(options);

        _options = options;
        _httpClient = new System.Net.Http.HttpClient
        {
            Timeout = _options.HttpTimeout
        };

        _formatter = new WebhookFormatter(_options);
        _jsonOptions = new System.Text.Json.JsonSerializerOptions
        {
            WriteIndented = false,
            PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.SnakeCaseLower,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        };

        _currentWebhookIndex = 0;
        _random = new System.Random();
    }

    #endregion Constructors

    #region API

    /// <summary>
    /// Sends a batch of log entries to Discord webhook.
    /// </summary>
    /// <param name="entries">The log entries to send.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True if the request succeeded; otherwise, false.</returns>
    public async System.Threading.Tasks.Task<System.Boolean> SendAsync(
        System.Collections.Generic.List<LogEntry> entries,
        System.Threading.CancellationToken cancellationToken)
    {
        if (_disposed || entries.Count is 0)
        {
            return false;
        }

        WebhookPayload payload = _formatter.Format(entries);
        System.String json = System.Text.Json.JsonSerializer.Serialize(payload, _jsonOptions);
        System.Net.Http.StringContent content = new(json, System.Text.Encoding.UTF8, "application/json");

        // Try all webhooks if needed (for Failover strategy)
        System.Int32 webhookCount = _options.WebhookUrls.Count;
        System.Int32 startIndex = SelectWebhookIndex();

        for (System.Int32 webhookAttempt = 0; webhookAttempt < webhookCount; webhookAttempt++)
        {
            System.Int32 webhookIndex = (startIndex + webhookAttempt) % webhookCount;
            System.String webhookUrl = _options.WebhookUrls[webhookIndex];

            // Retry logic with exponential backoff for current webhook
            for (System.Int32 attempt = 0; attempt <= _options.RetryCount; attempt++)
            {
                try
                {
                    System.Net.Http.HttpResponseMessage response = await _httpClient.PostAsync(webhookUrl, content, cancellationToken)
                                                                                    .ConfigureAwait(false);

                    if (response.IsSuccessStatusCode)
                    {
                        return true;
                    }

                    // Handle rate limiting (429)
                    if (response.StatusCode is System.Net.HttpStatusCode.TooManyRequests)
                    {
                        System.Diagnostics.Debug.WriteLine(
                            $"[LG. WebhookHttpClient] Webhook {webhookIndex + 1}/{webhookCount} rate limited (429)");

                        // Try next webhook immediately if available
                        if (_options.DispatchMode is WebhookDispatchMode.Failover
                            && webhookAttempt < webhookCount - 1)
                        {
                            break; // Break retry loop, try next webhook
                        }

                        if (attempt < _options.RetryCount)
                        {
                            System.TimeSpan delay = _options.RetryDelay * System.Math.Pow(2, attempt);
                            await System.Threading.Tasks.Task.Delay(delay, cancellationToken)
                                                             .ConfigureAwait(false);
                            continue;
                        }
                    }

                    // Other errors
                    System.Diagnostics.Debug.WriteLine(
                        $"[LG.WebhookHttpClient] Webhook {webhookIndex + 1}/{webhookCount} HTTP {(System.Int32)response.StatusCode}:  {response.ReasonPhrase}");

                    if (attempt < _options.RetryCount)
                    {
                        await System.Threading.Tasks.Task.Delay(_options.RetryDelay, cancellationToken)
                                                         .ConfigureAwait(false);
                    }
                }
                catch (System.Exception ex) when (ex is not System.OperationCanceledException)
                {
                    System.Diagnostics.Debug.WriteLine(
                        $"[LG.WebhookHttpClient] Webhook {webhookIndex + 1}/{webhookCount} Exception: {ex.Message}");

                    if (attempt < _options.RetryCount)
                    {
                        await System.Threading.Tasks.Task.Delay(_options.RetryDelay, cancellationToken)
                                                         .ConfigureAwait(false);
                    }
                }
            }

            // If not using Failover strategy, don't try other webhooks
            if (_options.DispatchMode is not WebhookDispatchMode.Failover)
            {
                break;
            }
        }

        return false;
    }

    /// <summary>
    /// Releases all resources used by the client.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _httpClient.Dispose();
    }

    #endregion API

    #region Private Methods

    /// <summary>
    /// Gets the next webhook index based on the configured load balancing strategy.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private System.Int32 SelectWebhookIndex()
    {
        return _options.DispatchMode switch
        {
            WebhookDispatchMode.RoundRobin => SelectRoundRobin(),
            WebhookDispatchMode.Random => SelectRandom(),
            WebhookDispatchMode.Failover => 0,
            _ => 0
        };
    }

    /// <summary>
    /// Gets the next webhook index using round-robin strategy.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private System.Int32 SelectRoundRobin()
    {
        System.Int32 index = System.Threading.Interlocked.Increment(ref _currentWebhookIndex);
        return System.Math.Abs(index % _options.WebhookUrls.Count);
    }

    /// <summary>
    /// Gets a random webhook index.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private System.Int32 SelectRandom()
    {
        lock (_random)
        {
            return _random.Next(_options.WebhookUrls.Count);
        }
    }

    #endregion Private Methods
}