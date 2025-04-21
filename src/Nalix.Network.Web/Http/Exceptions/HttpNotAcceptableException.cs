using System.Net;

namespace Nalix.Network.Web.Http.Exceptions;

/// <summary>
/// When thrown, breaks the request handling control flow
/// and sends a redirection response to the client.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="HttpNotAcceptableException"/> class.
/// </remarks>
/// <param name="vary">
/// <para>A value, or a comma-separated list of values, to set the response's <c>Vary</c> header to.</para>
/// <para>Although not specified in <see href="https://tools.ietf.org/html/rfc7231#section-6.5.6">RFC7231</see>,
/// this may help the client to understand why the request has been rejected.</para>
/// <para>If this parameter is <see langword="null"/> or the empty string, the response's <c>Vary</c> header
/// is not set.</para>
/// </param>
public class HttpNotAcceptableException(string? vary) : HttpException((int)HttpStatusCode.NotAcceptable)
{
    /// <summary>
    /// Initializes a new instance of the <see cref="HttpNotAcceptableException"/> class,
    /// without specifying a value for the response's <c>Vary</c> header.
    /// </summary>
    public HttpNotAcceptableException()
        : this(null)
    {
    }

    /// <summary>
    /// Gets the value, or comma-separated list of values, to be set
    /// on the response's <c>Vary</c> header.
    /// </summary>
    /// <remarks>
    /// <para>If the empty string has been passed to the <see cref="HttpNotAcceptableException(string)"/>
    /// constructor, the value of this property is <see langword="null"/>.</para>
    /// </remarks>
    public string? Vary { get; } = string.IsNullOrEmpty(vary) ? null : vary;

    /// <inheritdoc />
    public override void PrepareResponse(IHttpContext context)
    {
        if (Vary != null)
        {
            context.Response.Headers.Add(HttpHeaderNames.Vary, Vary);
        }
    }
}