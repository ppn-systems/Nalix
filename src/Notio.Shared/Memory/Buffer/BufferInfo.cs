namespace Notio.Shared.Memory.Buffer;

/// <summary>
/// Information about the Buffer Pool
/// </summary>
public readonly record struct BufferInfo
{
    /// <summary>
    /// Number of free buffers
    /// </summary>
    public required int FreeBuffers { get; init; }

    /// <summary>
    /// Total number of buffers
    /// </summary>
    public required int TotalBuffers { get; init; }

    /// <summary>
    /// Size of the buffer
    /// </summary>
    public required int BufferSize { get; init; }

    /// <summary>
    /// Number of misses
    /// </summary>
    public required int Misses { get; init; }
}