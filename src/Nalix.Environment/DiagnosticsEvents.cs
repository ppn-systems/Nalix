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
        /// Fired when the configuration context cache is managed (e.g. cleared due to capacity).
        /// </summary>
        public const string Cache = "Configuration.Cache";

        /// <summary>
        /// Fired when a configuration directory is created or verified.
        /// </summary>
        public const string Directory = "Configuration.Directory";

        /// <summary>
        /// Fired when any configuration-related error or exception occurs.
        /// This serves as a unified failure event.
        /// </summary>
        public const string Failure = "Configuration.Failure";
    }

    /// <summary>
    /// IO-related diagnostic events.
    /// </summary>
    public static class IO
    {
        /// <summary>
        /// Fired when a directory is created or accessed.
        /// </summary>
        public const string Directory = "IO.Directory";

        /// <summary>
        /// Fired when old files are deleted during cleanup.
        /// </summary>
        public const string Cleanup = "IO.Cleanup";
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

    /// <summary>
    /// Time-related diagnostic events.
    /// </summary>
    public static class Time
    {
        /// <summary>
        /// Fired when the clock is synchronized with an external source.
        /// </summary>
        public const string Synchronized = "Time.Synchronized";

        /// <summary>
        /// Fired when the clock synchronization is reset.
        /// </summary>
        public const string Reset = "Time.Reset";
    }
}
