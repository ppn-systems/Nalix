using System;
using System.Net;

namespace Notio.Web;

/// <summary>
/// When thrown, breaks the request handling control flow
/// and sends an error response to the client.
/// </summary>
public partial class HttpException : Exception, IHttpException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="HttpException"/> class,
    /// with no message to include in the response.
    /// </summary>
    /// <param name="statusCode">The status code to set on the response.</param>
    public HttpException(int statusCode)
    {
        StatusCode = statusCode;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="HttpException"/> class,
    /// with no message to include in the response.
    /// </summary>
    /// <param name="statusCode">The status code to set on the response.</param>
    public HttpException(HttpStatusCode statusCode)
        : this((int)statusCode)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="HttpException"/> class,
    /// with a message to include in the response.
    /// </summary>
    /// <param name="statusCode">The status code to set on the response.</param>
    /// <param name="message">A message to include in the response as plain text.</param>
    public HttpException(int statusCode, string? message)
        : base(message)
    {
        StatusCode = statusCode;
        HttpExceptionMessage = message;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="HttpException"/> class,
    /// with a message to include in the response.
    /// </summary>
    /// <param name="statusCode">The status code to set on the response.</param>
    /// <param name="message">A message to include in the response as plain text.</param>
    public HttpException(HttpStatusCode statusCode, string? message)
        : this((int)statusCode, message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="HttpException" /> class,
    /// with a message and a data object to include in the response.
    /// </summary>
    /// <param name="statusCode">The status code to set on the response.</param>
    /// <param name="message">A message to include in the response as plain text.</param>
    /// <param name="data">The data object to include in the response.</param>
    public HttpException(int statusCode, string? message, object? data)
        : this(statusCode, message)
    {
        DataObject = data;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="HttpException" /> class,
    /// with a message and a data object to include in the response.
    /// </summary>
    /// <param name="statusCode">The status code to set on the response.</param>
    /// <param name="message">A message to include in the response as plain text.</param>
    /// <param name="data">The data object to include in the response.</param>
    public HttpException(HttpStatusCode statusCode, string? message, object? data)
        : this((int)statusCode, message, data)
    {
    }

    /// <inheritdoc />
    public int StatusCode { get; }

    /// <inheritdoc />
    public object? DataObject { get; }

    /// <inheritdoc />
    string? IHttpException.Message => HttpExceptionMessage;

    // This property is necessary because when an exception with a null Message is thrown
    // the CLR provides a standard message. We want null to remain null in IHttpException.
    private string? HttpExceptionMessage { get; }

    /// <inheritdoc />
    public override string StackTrace => base.StackTrace ?? string.Empty;

    /// <inheritdoc />
    /// <remarks>
    /// <para>This method does nothing; there is no need to call
    /// <c>base.PrepareResponse</c> in overrides of this method.</para>
    /// </remarks>
    public virtual void PrepareResponse(IHttpContext context)
    {
    }
}