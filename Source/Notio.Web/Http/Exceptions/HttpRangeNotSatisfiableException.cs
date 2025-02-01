using System.Net;

namespace Notio.Network.Web.Http.Exceptions;

/// <summary>
/// When thrown, breaks the request handling control flow
/// and sends a redirection response to the client.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="HttpRangeNotSatisfiableException"/> class.
/// </remarks>
/// <param name="contentLength">The total length of the requested resource, expressed in bytes,
/// or <see langword="null"/> to omit the <c>Content-Range</c> header in the response.</param>
public class HttpRangeNotSatisfiableException(long? contentLength) : HttpException((int)HttpStatusCode.RequestedRangeNotSatisfiable)
{
    /// <summary>
    /// Initializes a new instance of the <see cref="HttpRangeNotSatisfiableException"/> class.
    /// without specifying a value for the response's <c>Content-Range</c> header.
    /// </summary>
    public HttpRangeNotSatisfiableException()
        : this(null)
    {
    }

    /// <summary>
    /// Gets the total content length to be specified
    /// on the response's <c>Content-Range</c> header.
    /// </summary>
    public long? ContentLength { get; } = contentLength;

    /// <inheritdoc />
    public override void PrepareResponse(IHttpContext context)
    {
        // RFC 7233, Section 3.1: "When this status code is generated in response
        //                        to a byte-range request, the sender
        //                        SHOULD generate a Content-Range header field specifying
        //                        the current length of the selected representation."
        if (ContentLength.HasValue)
        {
            context.Response.Headers.Set(HttpHeaderNames.ContentRange, $"bytes */{ContentLength.Value}");
        }
    }
}