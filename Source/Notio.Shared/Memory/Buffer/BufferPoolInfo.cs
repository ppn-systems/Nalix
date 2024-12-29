namespace Notio.Shared.Memory.Buffer;

/// <summary>
/// Thông tin về Buffer Pool
/// </summary>
public readonly record struct BufferPoolInfo
{
    /// <summary>
    /// Số lượng bộ đệm tự do
    /// </summary>
    public required int FreeBuffers { get; init; }

    /// <summary>
    /// Tổng số bộ đệm
    /// </summary>
    public required int TotalBuffers { get; init; }

    /// <summary>
    /// Kích thước bộ đệm
    /// </summary>
    public required int BufferSize { get; init; }

    /// <summary>
    /// Số lần bỏ lỡ
    /// </summary>
    public required int Misses { get; init; }
}
