using System.Diagnostics;

namespace Nalix.Environment;

/// <summary>
/// Central registry for all diagnostic event names used within the Environment module.
/// 
/// Design goals:
/// - Provide strongly-typed event names (avoid magic strings)
/// - Keep naming consistent across modules
/// - Reduce duplication of prefixes (namespace already scopes context)
/// </summary>
public static class DiagnosticsEvents
{
    /// <summary>
    /// The name of the <see cref="DiagnosticListener"/> used by the Environment module.
    /// Consumers can subscribe to this listener to observe all emitted events.
    /// </summary>
    public const string ListenerName = "Environment";

    /// <summary>
    /// Global diagnostic source for emitting events.
    /// </summary>
    public static readonly DiagnosticListener Source = new(ListenerName);

    /// <summary>
    /// Configuration-related diagnostic events.
    /// </summary>
    public static class Configuration
    {
        /// <summary>
        /// Fired when the active configuration path changes.
        /// Payload may include: oldPath, newPath.
        /// </summary>
        public const string PathChanged = "Configuration.PathChanged";

        /// <summary>
        /// Fired when configuration reload occurs.
        /// Possible states: Success, PartialSuccess, Background.
        /// </summary>
        public const string Reload = "Configuration.Reload";

        /// <summary>
        /// Fired when configuration container lifecycle changes.
        /// Actions: Created, Removed, Cleared.
        /// </summary>
        public const string Container = "Configuration.Container";

        /// <summary>
        /// Fired when configuration is flushed to persistent storage.
        /// </summary>
        public const string Flush = "Configuration.Flush";

        /// <summary>
        /// Fired when any configuration-related error or exception occurs.
        /// This serves as a unified failure event.
        /// </summary>
        public const string Failure = "Configuration.Failure";
    }

    /// <summary>
    /// Random-related diagnostic events.
    /// </summary>
    public static class Random
    {
        /// <summary>
        /// Fired when the random subsystem is initialized.
        /// </summary>
        public const string Init = "Random.Init";
    }
}
