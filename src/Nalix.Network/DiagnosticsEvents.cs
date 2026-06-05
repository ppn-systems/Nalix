using System.Diagnostics;

namespace Nalix.Network;

/// <summary>
/// Provides the diagnostic event source and event-name registry for the
/// <c>Nalix.Network</c> module.
/// </summary>
/// <remarks>
/// Network components should emit events through <see cref="Source"/> instead
/// of depending directly on a logging abstraction. Host/runtime layers may
/// subscribe to this listener and bridge events to logging, metrics, tracing,
/// or telemetry systems.
/// </remarks>
public static class DiagnosticsEvents
{
    /// <summary>
    /// The diagnostic listener name used by the Network module.
    /// </summary>
    public const string ListenerName = "Network";

    /// <summary>
    /// The shared diagnostic listener used to publish Network diagnostic events.
    /// </summary>
    /// <remarks>
    /// Hot paths should always call <see cref="DiagnosticListener.IsEnabled(string)"/>
    /// before allocating event payload objects.
    /// </remarks>
    public static readonly DiagnosticListener Source =
        Environment.Diagnostics.DiagnosticListenerFactory.Create(ListenerName);

    /// <summary>
    /// Diagnostic event names related to listener lifecycle and accept/bind operations.
    /// </summary>
    public static class Listeners
    {
        /// <summary>
        /// Raised when a network listener has started successfully.
        /// </summary>
        public const string Started = "Listeners.Started";

        /// <summary>
        /// Raised when a network listener has stopped.
        /// </summary>
        public const string Stopped = "Listeners.Stopped";

        /// <summary>
        /// Raised when a listener fails to bind to its configured endpoint.
        /// </summary>
        public const string BindFailed = "Listeners.BindFailed";

        /// <summary>
        /// Raised when a listener accept loop fails while accepting an incoming connection.
        /// </summary>
        public const string AcceptFailed = "Listeners.AcceptFailed";
    }

    /// <summary>
    /// Diagnostic event names related to connection lifecycle.
    /// </summary>
    public static class Connections
    {
        /// <summary>
        /// Raised when a connection has been accepted or opened.
        /// </summary>
        public const string Opened = "Connections.Opened";

        /// <summary>
        /// Raised when a connection has been closed.
        /// </summary>
        public const string Closed = "Connections.Closed";

        /// <summary>
        /// Raised when a connection is rejected before being fully accepted.
        /// </summary>
        public const string Rejected = "Connections.Rejected";

        /// <summary>
        /// Raised when a connection times out.
        /// </summary>
        public const string Timeout = "Connections.Timeout";
    }

    /// <summary>
    /// Diagnostic event names related to transport-level socket and frame operations.
    /// </summary>
    public static class Transport
    {
        /// <summary>
        /// Raised when a transport receive operation fails.
        /// </summary>
        public const string ReceiveFailed = "Transport.ReceiveFailed";

        /// <summary>
        /// Raised when a transport send operation fails.
        /// </summary>
        public const string SendFailed = "Transport.SendFailed";

        /// <summary>
        /// Raised when an incoming frame is malformed or cannot be decoded.
        /// </summary>
        public const string MalformedFrame = "Transport.MalformedFrame";

        /// <summary>
        /// Raised when an incoming frame exceeds the configured transport or framing limit.
        /// </summary>
        public const string OversizedFrame = "Transport.OversizedFrame";

        /// <summary>
        /// Raised when a socket-level error occurs.
        /// </summary>
        public const string SocketError = "Transport.SocketError";

        /// <summary>
        /// Raised when the remote endpoint disconnects or the transport observes a disconnect.
        /// </summary>
        public const string Disconnected = "Transport.Disconnected";
    }

    /// <summary>
    /// Diagnostic event names related to network protection, filtering, and abuse control.
    /// </summary>
    public static class Security
    {
        /// <summary>
        /// Raised when traffic from an endpoint is rejected or delayed by rate limiting.
        /// </summary>
        public const string RateLimited = "Security.RateLimited";

        /// <summary>
        /// Raised when an endpoint matches a blacklist rule.
        /// </summary>
        public const string Blacklisted = "Security.Blacklisted";

        /// <summary>
        /// Raised when an endpoint is banned or a ban rule is applied.
        /// </summary>
        public const string Banned = "Security.Banned";

        /// <summary>
        /// Raised when a packet or endpoint behavior is considered suspicious.
        /// </summary>
        public const string SuspiciousPacket = "Security.SuspiciousPacket";

        /// <summary>
        /// Raised when network traffic patterns indicate a possible denial-of-service attack.
        /// </summary>
        public const string DdosDetected = "Security.DdosDetected";

        /// <summary>
        /// Raised when a security or fairness limit is corrected after drifting from its expected state.
        /// </summary>
        public const string LimitDriftCorrected = "Security.LimitDriftCorrected";

        /// <summary>
        /// Raised when a security cleanup operation fails.
        /// </summary>
        public const string CleanupError = "Security.CleanupError";
    }

    /// <summary>
    /// Diagnostic event names for internal Network module faults, warnings, and traces.
    /// </summary>
    /// <remarks>
    /// These events are intended for infrastructure diagnostics and should not
    /// expose raw packet payloads, secrets, keys, tokens, or sensitive user data.
    /// </remarks>
    public static class Internal
    {
        /// <summary>
        /// Raised when an internal network loop faults unexpectedly.
        /// </summary>
        public const string LoopFaulted = "Internal.LoopFaulted";

        /// <summary>
        /// Raised when an internal network resource is exhausted.
        /// </summary>
        public const string ResourceExhausted = "Internal.ResourceExhausted";

        /// <summary>
        /// Raised for internal warning-level diagnostic messages.
        /// </summary>
        public const string Warning = "Internal.Warning";

        /// <summary>
        /// Raised for internal critical diagnostic messages.
        /// </summary>
        public const string Critical = "Internal.Critical";

        /// <summary>
        /// Raised for internal debug-level diagnostic messages.
        /// </summary>
        public const string Debug = "Internal.Debug";

        /// <summary>
        /// Raised for internal trace-level diagnostic messages.
        /// </summary>
        public const string Trace = "Internal.Trace";

        /// <summary>
        /// Raised for internal error-level diagnostic messages.
        /// </summary>
        public const string Error = "Internal.Error";

        /// <summary>
        /// Raised for internal informational diagnostic messages.
        /// </summary>
        public const string Information = "Internal.Information";
    }
}
