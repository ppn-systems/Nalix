using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Notio.Http;

public static class HttpExtensions
{
    public record ApiResponse<T>(HttpStatusCode StatusCode, T Data = default, string Error = null);

    public static async Task SendResponseAsync<T>(this HttpListenerResponse response, ApiResponse<T> apiResponse)
    {
        response.StatusCode = (int)apiResponse.StatusCode;
        await using var stream = response.OutputStream;
        await JsonSerializer.SerializeAsync(stream, apiResponse);
    }

    public static async Task<T> DeserializeRequestAsync<T>(this Stream inputStream)
    {
        try
        {
            return await JsonSerializer.DeserializeAsync<T>(inputStream);
        }
        catch
        {
            return default;
        }
    }

    public static async Task WriteJsonResponseAsync<T>(this HttpListenerResponse response, T data)
    {
        try
        {
            string json = System.Text.Json.JsonSerializer.Serialize(data);
            byte[] buffer = Encoding.UTF8.GetBytes(json);

            response.ContentType = "application/json";
            response.ContentLength64 = buffer.Length;

            await response.OutputStream.WriteAsync(buffer.AsMemory());
        }
        finally
        {
            response.OutputStream.Close();
        }
    }
}
