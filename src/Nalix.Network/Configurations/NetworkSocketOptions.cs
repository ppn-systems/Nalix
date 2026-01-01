// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Framework.Configuration.Binding;

namespace Nalix.Network.Configurations;

/// <summary>
/// Represents network configuration settings for socket and RELIABLE connections.
/// </summary>
public sealed class NetworkSocketOptions : ConfigurationLoader
{
    #region Fields

    private System.UInt16 _port = 57206;

    #endregion Fields

    #region Constants

    /// <summary>
    /// Constant value for True (1).
    /// </summary>
    internal const System.Int32 True = 1;

    /// <summary>
    /// Constant value for False (0).
    /// </summary>
    internal const System.Int32 False = 0;

    #endregion Constants

    #region Properties

    /// <summary>
    /// Gets or sets the port number for the network connection.
    /// Must be within the range of 1 to 65535.
    /// Standard is 57206.
    /// </summary>
    public System.UInt16 Port
    {
        get => this._port;
        set
        {
            if (value is <= System.UInt16.MinValue or > System.UInt16.MaxValue)
            {
                throw new System.ArgumentOutOfRangeException(nameof(value), "Port must be between 1 and 65535.");
            }

            this._port = value;
        }
    }

    /// <summary>
    /// Gets or sets the maximum length of the pending connections queue.
    /// Default is 512.
    /// </summary>
    public System.Int32 Backlog { get; set; } = 512;

    /// <summary>
    /// Gets or sets a value indicating whether the idle timeout mechanism is enabled.
    /// </summary>
    /// <value>
    /// <c>true</c> to enable idle timeout monitoring; otherwise, <c>false</c>.
    /// </value>
    public System.Boolean EnableTimeout { get; set; } = true;

    /// <summary>
    /// Indicates whether to use IPv4 or IPv6.
    /// </summary>
    public System.Boolean EnableIPv6 { get; set; } = false;

    /// <summary>
    /// Gets or sets whether Nagle's algorithm is disabled (low-latency communication).
    /// Standard is true.
    /// </summary>
    public System.Boolean NoDelay { get; set; } = true;

    /// <summary>
    /// Gets or sets the maximum number of parallel connections.
    /// </summary>
    public System.Int32 MaxParallel { get; set; } = 5;

    /// <summary>
    /// Gets or sets the buffer size for both sending and receiving data.
    /// </summary>
    /// <value>
    /// The buffer size in bytes. Default is <c>8192</c>.
    /// </value>
    public System.Int32 BufferSize { get; set; } = 4 * 1024;

    /// <summary>
    /// Gets or sets a value indicating whether the socket should use the RELIABLE Keep-Alive mechanism.
    /// </summary>
    /// <value>
    /// <c>true</c> to enable Keep-Alive; otherwise, <c>false</c>.
    /// </value>
    public System.Boolean KeepAlive { get; set; } = false;

    /// <summary>
    /// Gets or sets whether the socket can reuse an address already in the TIME_WAIT state.
    /// Standard is false.
    /// </summary>
    public System.Boolean ReuseAddress { get; set; } = true;

    /// <summary>
    /// Gets or sets the maximum number of concurrent groups allowed for socket operations.
    /// Default is 8.
    /// </summary>
    public System.Int32 MaxGroupConcurrency { get; set; } = 8;

    /// <summary>
    /// Tunes the thread pool settings for optimal network performance.
    /// </summary>
    public System.Boolean TuneThreadPool { get; set; } = false;

    #endregion Properties
}