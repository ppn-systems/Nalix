namespace Notio.Http.Enums;

/// <summary>
/// Types of events raised by Flurl over the course of a call that can be handled via event handlers.
/// </summary>
public enum HttpEventType
{
    /// <summary>
    /// Fired immediately before an HTTP request is sent.
    /// </summary>
    BeforeCall,

    /// <summary>
    /// Fired immediately after an HTTP response is received.
    /// </summary>
    AfterCall,

    /// <summary>
    /// Fired when an HTTP error response is received, just before AfterCall is fired. Error
    /// responses include any status in 4xx or 5xx range by default, configurable via AllowHttpStatus.
    /// You can inspect call.Exception for details, and optionally set call.ExceptionHandled to
    /// true to prevent the exception from bubbling up after the handler exits.
    /// </summary>
    OnError,

    /// <summary>
    /// Fired when any 3xx response with a Location header is received, just before AfterCall is fired
    /// and before the subsequent (redirected) request is sent. You can inspect/manipulate the
    /// call.Redirect object to determine what will happen next. An auto-redirect will only happen if
    /// call.Redirect.Follow is true upon exiting the callback.
    /// </summary>
    OnRedirect
}