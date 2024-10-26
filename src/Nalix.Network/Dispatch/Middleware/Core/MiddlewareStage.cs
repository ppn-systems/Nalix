namespace Nalix.Network.Dispatch.Middleware.Core;


/// <summary>
/// Represents the stage at which middleware is executed in the dispatch pipeline.
/// </summary>
public enum MiddlewareStage : System.Byte
{
    /// <summary>
    /// Indicates the middleware is executed before the main operation.
    /// </summary>
    Pre,
    /// <summary>
    /// Indicates the middleware is executed after the main operation.
    /// </summary>
    Post
}
