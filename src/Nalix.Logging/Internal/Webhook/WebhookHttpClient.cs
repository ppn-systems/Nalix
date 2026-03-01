// Copyright (c) 2025-2026 PPN Corporation. All rights reserved. 

using Nalix.Common.Diagnostics;
using Nalix.Common.Enums;
using Nalix.Framework.Random;
using Nalix.Logging.Configuration;
using Nalix.Logging.Internal.Formatters;
using Nalix.Logging.Internal.Webhook.Models;

#if DEBUG
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Nalix.Logging.Tests")]
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Nalix.Logging.Benchmarks")]
#endif

namespace Nalix.Logging.Internal.Webhook;

/// <summary>
/// Handles HTTP communication with Discord webhook API.
/// Manages retry logic, rate limiting, and payload formatting.
/// </summary>
internal sealed class WebhookHttpClient : System.IDisposable
{
    #region Fields

    private volatile System.Boolean _disposed;
    private readonly WebhookLogOptions _options;
    private readonly WebhookFormatter _formatter;
    private readonly System.Net.Http.HttpClient _httpClient;

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
        _currentWebhookIndex = 0;
        _formatter = new WebhookFormatter(_options);
        _httpClient = new System.Net.Http.HttpClient
        {
            Timeout = _options.HttpTimeout
        };
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
        if (_disposed || entries.Count == 0)
        {
            return false;
        }

        WebhookPayload payload = _formatter.Format(entries);

        // Serialize to JSON using our manual serializer
        System.String json = payload.ToJson();

        // Try all webhooks if needed (for Failover strategy)
        System.Int32 startIndex = SelectWebhookIndex();
        System.Int32 webhookCount = _options.WebhookUrls.Count;

        for (System.Int32 webhookAttempt = 0; webhookAttempt < webhookCount; webhookAttempt++)
        {
            System.Int32 webhookIndex = (startIndex + webhookAttempt) % webhookCount;
            System.String webhookUrl = _options.WebhookUrls[webhookIndex];

            // Retry logic with exponential backoff for current webhook
            for (System.Int32 attempt = 0; attempt <= _options.RetryCount; attempt++)
            {
                try
                {
                    // Create a fresh HttpContent for each attempt to avoid reusing disposed content
                    using System.Net.Http.StringContent content = new(json, System.Text.Encoding.UTF8, "application/json");

                    System.Net.Http.HttpResponseMessage response = await _httpClient.PostAsync(webhookUrl, content, cancellationToken)
                                                                   .ConfigureAwait(false);

                    if (response.IsSuccessStatusCode)
                    {
                        return true;
                    }

                    // Handle rate limiting (429)
                    if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
                    {
                        System.Diagnostics.Debug.WriteLine(
                            $"[LG.WebhookHttpClient] Webhook {webhookIndex + 1}/{webhookCount} rate limited (429)");

                        // Try next webhook immediately if available
                        if (_options.DispatchMode == WebhookDispatchMode.Failover
                            && webhookAttempt < webhookCount - 1)
                        {
                            break; // Break retry loop, try next webhook
                        }

                        if (attempt < _options.RetryCount)
                        {
                            System.TimeSpan delay = System.TimeSpan.FromMilliseconds(_options.RetryDelay.TotalMilliseconds * System.Math.Pow(2, attempt));
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
            if (_options.DispatchMode != WebhookDispatchMode.Failover)
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
            WebhookDispatchMode.Failover => 0,
            WebhookDispatchMode.Random => Csprng.GetInt32(_options.WebhookUrls.Count),
            WebhookDispatchMode.RoundRobin => SelectRoundRobin(),
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

    #endregion Private Methods
}