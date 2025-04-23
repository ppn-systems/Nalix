using Nalix.Environment;
using Nalix.Network.Web.Http.Extensions;
using Nalix.Network.Web.MimeTypes;
using System.Text.Json;
using System.Threading.Tasks;

namespace Nalix.Network.Web.Http.Response
{
    /// <summary>
    /// Provides standard response serializer callbacks.
    /// </summary>
    /// <seealso cref="ResponseSerializerCallback"/>
    public static class ResponseSerializer
    {
        /// <summary>
        /// <para>The default response serializer callback used by Notio.</para>
        /// <para>Equivalent to <see cref="Json(IHttpContext,object?)">Serialization</see>.</para>
        /// </summary>
        public static readonly ResponseSerializerCallback Default = Json;

        private static readonly ResponseSerializerCallback ChunkedEncodingBaseSerializer = GetBaseSerializer(false);
        private static readonly ResponseSerializerCallback BufferingBaseSerializer = GetBaseSerializer(true);

        /// <summary>
        /// Serializes data in JSON format to a HTTP response,
        /// </summary>
        /// <param name="context">The HTTP context of the request.</param>
        /// <param name="data">The data to serialize.</param>
        /// <returns>A <see cref="Task"/> representing the ongoing operation.</returns>
        public static async Task Json(IHttpContext context, object? data)
        {
            context.Response.ContentType = MimeType.Json;
            context.Response.ContentEncoding = WebServer.Utf8NoBomEncoding;
            await ChunkedEncodingBaseSerializer(context, JsonSerializer.Serialize(data, JsonOptions.HttpFormatted))
                .ConfigureAwait(false);
        }

        /// <summary>
        /// Serializes data in JSON format.
        /// </summary>
        /// <returns>A <see cref="ResponseSerializerCallback"/> that can be used to serialize
        /// data to a HTTP response.</returns>
        public static ResponseSerializerCallback Json()
        {
            return async (context, data) =>
                {
                    context.Response.ContentType = MimeType.Json;
                    context.Response.ContentEncoding = WebServer.Utf8NoBomEncoding;
                    await ChunkedEncodingBaseSerializer(context, JsonSerializer.Serialize(data, JsonOptions.HttpFormatted))
                        .ConfigureAwait(false);
                };
        }

        /// <summary>
        /// Serializes data in JSON format to a HTTP response.
        /// </summary>
        /// <param name="bufferResponse"><see langword="true"/> to write the response body to a memory buffer first,
        /// then send it all together with a <c>Content-Length</c> header; <see langword="false"/> to use chunked
        /// transfer encoding.</param>
        /// <returns>A <see cref="ResponseSerializerCallback"/> that can be used to serialize
        /// data to a HTTP response.</returns>
        public static ResponseSerializerCallback Json(bool bufferResponse)
            => async (context, data) =>
            {
                context.Response.ContentType = MimeType.Json;
                context.Response.ContentEncoding = WebServer.Utf8NoBomEncoding;
                ResponseSerializerCallback baseSerializer = None(bufferResponse);
                await baseSerializer(context, JsonSerializer.Serialize(data, JsonOptions.HttpFormatted))
                    .ConfigureAwait(false);
            };

        /// <summary>
        /// Sends data in a HTTP response without serialization.
        /// </summary>
        /// <param name="bufferResponse"><see langword="true"/> to write the response body to a memory buffer first,
        /// then send it all together with a <c>Content-Length</c> header; <see langword="false"/> to use chunked
        /// transfer encoding.</param>
        /// <returns>A <see cref="ResponseSerializerCallback"/> that can be used to serialize data to a HTTP response.</returns>
        /// <remarks>
        /// <para><see cref="string"/>s and one-dimensional arrays of <see cref="byte"/>s
        /// are sent to the client unchanged; every other type is converted to a string.</para>
        /// <para>The <see cref="IHttpResponse.ContentType">ContentType</see> set on the response is used to negotiate
        /// a compression method, according to request headers.</para>
        /// <para>Strings (and other types converted to strings) are sent with the encoding specified by <see cref="IHttpResponse.ContentEncoding"/>.</para>
        /// </remarks>
        public static ResponseSerializerCallback None(bool bufferResponse)
            => bufferResponse ? BufferingBaseSerializer : ChunkedEncodingBaseSerializer;

        private static ResponseSerializerCallback GetBaseSerializer(bool bufferResponse)
            => async (context, data) =>
            {
                if (data is null)
                {
                    return;
                }

                bool isBinaryResponse = data is byte[];

                if (!context.TryDetermineCompression(context.Response.ContentType, out bool preferCompression))
                {
                    preferCompression = true;
                }

                if (isBinaryResponse)
                {
                    byte[] responseBytes = (byte[])data;
                    using System.IO.Stream stream = context.OpenResponseStream(bufferResponse, preferCompression);
                    await stream.WriteAsync(responseBytes).ConfigureAwait(false);
                }
                else
                {
                    string responseString = data is string stringData ? stringData : data.ToString() ?? string.Empty;
                    using System.IO.TextWriter text = context.OpenResponseText(context.Response.ContentEncoding, bufferResponse, preferCompression);
                    await text.WriteAsync(responseString).ConfigureAwait(false);
                }
            };
    }
}
