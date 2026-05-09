// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

namespace Nalix.SDK.Native;

/// <summary>
/// Provides the entry point symbols for interacting with the Nalix native library.
/// </summary>
/// <remarks>
/// This class centralizes all unmanaged function names to ensure consistency across P/Invoke and dynamic loading logic.
/// </remarks>
public static class NativeMethods
{
    /// <summary>
    /// Contains native entry points for the TCP protocol.
    /// </summary>
    public static class Tcp
    {
        /// <summary>The symbol name for creating a new native TCP session.</summary>
        public const string Create = "nalix_tcp_create";

        /// <summary>The symbol name for initiating a connection to a remote TCP endpoint.</summary>
        public const string Connect = "nalix_tcp_connect";

        /// <summary>The symbol name for sending data through an established TCP stream.</summary>
        public const string Send = "nalix_tcp_send";

        /// <summary>The symbol name for performing a protocol-specific handshake.</summary>
        public const string Handshake = "nalix_tcp_handshake";

        /// <summary>The symbol name for disconnect to TCP endpoint.</summary>
        public const string Disconnect = "nalix_tcp_disconnect";

        /// <summary>The symbol name for closing and releasing native TCP resources.</summary>
        public const string Free = "nalix_tcp_free";

        /// <summary>
        /// Graceful disconnect with reason code.
        /// </summary>
        public const string DisconnectGraceful = "nalix_tcp_disconnect_graceful";

        /// <summary>
        /// Send PING and measure RTT.
        /// </summary>
        public const string Ping = "nalix_tcp_ping";

        /// <summary>
        /// Perform time synchronization with server.
        /// </summary>
        public const string SyncTime = "nalix_tcp_sync_time";

        /// <summary>
        /// Resume existing session using saved token + secret.
        /// </summary>
        public const string ResumeSession = "nalix_tcp_resume_session";

        /// <summary>
        /// Connect + attempt resume (with fallback to handshake).
        /// </summary>
        public const string ConnectWithResume = "nalix_tcp_connect_with_resume";

        /// <summary>
        /// Update active cipher suite at runtime.
        /// </summary>
        public const string UpdateCipher = "nalix_tcp_update_cipher";

        /// <summary>
        /// Send a CONTROL frame with basic parameters.
        /// </summary>
        public const string SendControl = "nalix_tcp_send_control";

        /// <summary>
        /// Provides the symbol names for TCP event callback registrations.
        /// </summary>
        public static class Events
        {
            /// <summary>Symbol for the callback triggered upon successful connection.</summary>
            public const string OnConnected = "nalix_tcp_on_connected";

            /// <summary>Symbol for the callback triggered when a message is received.</summary>
            public const string OnMessage = "nalix_tcp_on_message";

            /// <summary>Symbol for the callback triggered when a transport error occurs.</summary>
            public const string OnError = "nalix_tcp_on_error";

            /// <summary>Symbol for the callback triggered when the session is disconnected.</summary>
            public const string OnDisconnected = "nalix_tcp_on_disconnected";
        }
    }

    /// <summary>
    /// Last error retrieval functions for the native library.
    /// </summary>
    public static class LastError
    {
        /// <summary>
        /// Retrieves the last error message from the native library.
        /// </summary>
        public const string Get = "nalix_get_last_error";

        /// <summary>
        /// Frees the memory allocated for the last error message.
        /// </summary>
        public const string Free = "nalix_free_error";
    }
}
