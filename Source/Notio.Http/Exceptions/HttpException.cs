using System;
using System.Threading.Tasks;

namespace Notio.Http.Exceptions;

/// <summary>
/// An exception that is thrown when an HTTP call made by Flurl.Http fails, including when the response
/// indicates an unsuccessful HTTP status code.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="HttpException"/> class.
/// </remarks>
/// <param name="call">The call.</param>
/// <param name="message">The message.</param>
/// <param name="inner">The inner.</param>
public class HttpException(NotioCall call, string message, Exception inner) : Exception(message, inner)
{
    /// <summary>
    /// An object containing details about the failed HTTP call
    /// </summary>
    public NotioCall Call { get; } = call;

    /// <summary>
    /// Initializes a new instance of the <see cref="HttpException"/> class.
    /// </summary>
    /// <param name="call">The call.</param>
    /// <param name="inner">The inner.</param>
    public HttpException(NotioCall call, Exception inner) : this(call, BuildMessage(call, inner), inner) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="HttpException"/> class.
    /// </summary>
    /// <param name="call">The call.</param>
    public HttpException(NotioCall call) : this(call, BuildMessage(call, null), null) { }

    private static string BuildMessage(NotioCall call, Exception inner)
    {
        if (call?.Response != null && !call.Succeeded)
            return $"Call failed with status code {call.Response.StatusCode} ({call.HttpResponseMessage.ReasonPhrase}): {call}";

        var msg = "Call failed";
        if (inner != null) msg += ". " + inner.Message.TrimEnd('.');
        return msg + (call == null ? "." : $": {call}");
    }

    /// <summary>
    /// Gets the HTTP status code of the response if a response was received, otherwise null.
    /// </summary>
    public int? StatusCode => Call?.Response?.StatusCode;

    /// <summary>
    /// Gets the response body of the failed call.
    /// </summary>
    /// <returns>A task whose result is the string contents of the response body.</returns>
    public Task<string> GetResponseStringAsync() => Call?.Response?.GetStringAsync() ?? Task.FromResult((string)null);

    /// <summary>
    /// Deserializes the JSON response body to an object of the given type.
    /// </summary>
    /// <typeparam name="T">A type whose structure matches the expected JSON response.</typeparam>
    /// <returns>A task whose result is an object containing data in the response body.</returns>
    public Task<T> GetResponseJsonAsync<T>() => Call?.Response?.GetJsonAsync<T>() ?? Task.FromResult(default(T));
}