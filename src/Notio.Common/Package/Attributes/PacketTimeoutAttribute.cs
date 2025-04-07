namespace Notio.Common.Package.Attributes;

/// <summary>
/// An attribute that specifies the timeout duration for a packet operation.
/// This attribute can be applied to methods to define the maximum allowable time 
/// (in milliseconds) for processing a packet before considering it a timeout.
/// </summary>
/// <remarks>
/// This is useful for enforcing time constraints in packet processing, ensuring that
/// operations that take too long can be handled appropriately.
/// </remarks>
/// <remarks>
/// Initializes a new instance of the <see cref="PacketTimeoutAttribute"/> class.
/// </remarks>
/// <param name="timeoutMilliseconds">The timeout duration in milliseconds.</param>
[System.AttributeUsage(System.AttributeTargets.Method)]
public class PacketTimeoutAttribute(int timeoutMilliseconds) : System.Attribute
{
    /// <summary>
    /// Gets the timeout duration (in milliseconds) specified for the method.
    /// </summary>
    public int TimeoutMilliseconds { get; } = timeoutMilliseconds;
}
