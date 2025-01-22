using System;
using System.IO;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Notio.Http.Core;

public static class HttpUtilities
{
    private static readonly JsonSerializerOptions DefaultJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false
    };

    // Gửi phản hồi JSON đơn giản
    public static async Task WriteJsonResponseAsync<T>(
        this HttpListenerResponse response,
        HttpStatusCode statusCode,
        T data)
    {
        try
        {
            string json = JsonSerializer.Serialize(data, DefaultJsonOptions);
            byte[] buffer = Encoding.UTF8.GetBytes(json);

            response.StatusCode = (int)statusCode;
            response.ContentType = "application/json";
            response.ContentLength64 = buffer.Length;

            await response.OutputStream.WriteAsync(buffer.AsMemory());
        }
        finally
        {
            response.OutputStream.Close();
        }
    }

    // Gửi phản hồi lỗi
    public static async Task WriteErrorResponseAsync(
        this HttpListenerResponse response,
        HttpStatusCode statusCode,
        string errorMessage)
    {
        await response.WriteJsonResponseAsync(
            statusCode,
            new { Error = errorMessage }
        );
    }

    // Deserialize JSON từ luồng yêu cầu
    public static async Task<T> DeserializeRequestAsync<T>(this Stream inputStream)
    {
        try
        {
            return await JsonSerializer.DeserializeAsync<T>(inputStream, DefaultJsonOptions);
        }
        catch
        {
            return default;
        }
    }
}