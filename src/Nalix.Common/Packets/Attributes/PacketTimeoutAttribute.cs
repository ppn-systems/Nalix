namespace Nalix.Common.Packets.Attributes;

/// <summary>
/// Specifies the maximum allowed processing time, in milliseconds, for a packet-handling method.
/// </summary>
/// <remarks>
/// Apply this attribute to a packet handler method to define a time limit for processing.
/// If the operation exceeds the specified timeout, it can be treated as a failure or trigger
/// a timeout handling routine.
/// </remarks>
/// <param name="timeoutMilliseconds">
/// The timeout duration in milliseconds before the packet operation is considered to have timed out.
/// </param>
[System.AttributeUsage(System.AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class PacketTimeoutAttribute(System.Int32 timeoutMilliseconds) : System.Attribute
{
    /// <summary>
    /// Gets the timeout duration, in milliseconds, specified for the method.
    /// </summary>
    public System.Int32 TimeoutMilliseconds { get; } = timeoutMilliseconds;
}
