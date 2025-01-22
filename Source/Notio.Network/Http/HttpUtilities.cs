using System;
using System.IO;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Notio.Network.Http;

public static class HttpUtilities
{
    internal static readonly JsonSerializerOptions DefaultJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false
    };

    // Gửi phản hồi JSON (Chung cho cả dữ liệu thành công và lỗi)
    internal static async Task WriteJsonResponseAsyncInternal<T>(
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
        catch (Exception ex)
        {
            // Log exception nếu cần thiết.
            response.StatusCode = (int)HttpStatusCode.InternalServerError;
            await response.WriteJsonResponseAsync(
                HttpStatusCode.InternalServerError,
                new { Error = ex.Message }
            );
        }
        finally
        {
            response.OutputStream.Close();
        }
    }

    // Gửi phản hồi JSON thành công
    public static Task WriteJsonResponseAsync<T>(
        this HttpListenerResponse response,
        HttpStatusCode statusCode,
        T data)
        => response.WriteJsonResponseAsyncInternal(statusCode, data);

    // Gửi phản hồi lỗi với JSON
    public static Task WriteErrorResponseAsync<T>(
        this HttpListenerResponse response,
        HttpStatusCode statusCode,
        T data)
        => response.WriteJsonResponseAsyncInternal(statusCode, data);

    // Gửi phản hồi lỗi với thông báo chuỗi
    public static Task WriteErrorResponseAsync(
        this HttpListenerResponse response,
        HttpStatusCode statusCode,
        string errorMessage)
        => response.WriteJsonResponseAsync(
            statusCode,
            new { Error = errorMessage }
        );

    // Deserialize JSON từ luồng yêu cầu
    public static async Task<T?> DeserializeRequestAsync<T>(this Stream inputStream)
    {
        try
        {
            return await JsonSerializer.DeserializeAsync<T>(inputStream, DefaultJsonOptions);
        }
        catch (Exception)
        {
            // Log exception nếu cần thiết.
            return default;
        }
    }
}