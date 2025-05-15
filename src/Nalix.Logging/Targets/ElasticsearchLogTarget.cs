using Nalix.Common.Logging;
using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;

namespace Nalix.Logging.Targets;

internal class ElasticsearchLogTarget : ILoggerTarget
{
    private readonly HttpClient _httpClient;
    private readonly string _elasticsearchUrl;

    public ElasticsearchLogTarget(string elasticsearchUrl)
    {
        if (string.IsNullOrWhiteSpace(elasticsearchUrl))
            throw new ArgumentNullException(nameof(elasticsearchUrl));

        _elasticsearchUrl = elasticsearchUrl;
        _httpClient = new HttpClient();
    }

    public async void Publish(LogEntry entry)
    {
        StringContent content = new(BuildJsonPayload(entry), Encoding.UTF8, "application/json");

        try
        {
            using var response = await _httpClient.PostAsync(_elasticsearchUrl, content);

            if (!response.IsSuccessStatusCode)
            {
                Console.WriteLine($"[Logging] Failed to log to Elasticsearch: {response.StatusCode}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Logging] Error while logging to Elasticsearch: {ex.Message}");
        }
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0037:Use inferred member name", Justification = "<Pending>")]
    [System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage("Trimming",
        "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code", Justification = "<Pending>")]
    private static string BuildJsonPayload(LogEntry entry)
        => JsonSerializer.Serialize(new
        {
            Timestamp = DateTime.UtcNow.ToString("O"), // ISO 8601
            Level = entry.LogLevel.ToString(),
            Message = entry.Message,
            Exception = entry.Exception?.ToString(),
            EventId = entry.EventId.Id,
            Source = entry.EventId.Name
        });

    public void Dispose() => _httpClient.Dispose();
}
