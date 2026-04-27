using System.Runtime.CompilerServices;
using Nalix.Common.Abstractions;

namespace Nalix.Codec.DataFrames;

/// <summary>
/// Provides an optimized initialization and reclamation mechanism for Packets.
/// Automatically coordinates between renting from a pool (if configured) or creating via constructor.
/// </summary>
/// <typeparam name="T">The specific Packet type, must inherit from <see cref="PacketBase{T}"/>.</typeparam>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1000:Do not declare static members on generic types", Justification = "<Pending>")]
public static class PacketProvider<T> where T : PacketBase<T>, new()
{
    /// <summary>
    /// Creates or rents an instance of <typeparamref name="T"/>.
    /// </summary>
    /// <returns>A packet instance ready for use.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T Create()
    {
        // Use a local variable to optimize static field access
        IObjectPoolManager? mgr = PacketPoolRegistry.Manager;
        return mgr == null ? new T() : mgr.Get<T>();
    }

    /// <summary>
    /// Reclaims the packet and returns it to the pool if pooling is enabled.
    /// </summary>
    /// <param name="packet">The packet instance to reclaim.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Return(T packet)
    {
        IObjectPoolManager? mgr = PacketPoolRegistry.Manager;
        mgr?.Return(packet);
    }
}

/// <summary>
/// Provides a centralized storage for the <see cref="IObjectPoolManager"/>.
/// This class is internal to protect the pool state from unauthorized external modification.
/// </summary>
public static class PacketPoolRegistry
{
    /// <summary>
    /// A single instance of the Pool Manager shared across all packet types.
    /// If this is <see langword="null"/>, the system automatically falls back to standard allocation.
    /// </summary>
    internal static IObjectPoolManager? Manager;

    /// <summary>
    /// Configures the shared Pool Manager for the entire packet ecosystem.
    /// </summary>
    /// <param name="manager">An implementation of <see cref="IObjectPoolManager"/> from the infrastructure layer.</param>
    /// <remarks>
    /// Because <see cref="PacketPoolRegistry"/> is shared, calling Configure on any 
    /// <c>PacketProvider&lt;T&gt;</c> will take effect for all other packet types.
    /// </remarks>
    public static void Configure(IObjectPoolManager manager) => Manager = manager;
}
