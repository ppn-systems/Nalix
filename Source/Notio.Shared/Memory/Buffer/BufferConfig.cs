using Notio.Shared.Configuration;

namespace Notio.Shared.Memory.Buffer;

/// <summary>
/// Configuration for buffer settings.
/// </summary>
public sealed class BufferConfig : ConfigContainer
{
    /// <summary>
    /// Tổng số lượng buffers được tạo.
    /// </summary>
    public int TotalBuffers { get; private set; } = 100;

    /// <summary>
    /// Chuỗi phân bổ buffer dạng "kích thước, tỷ lệ; kích thước, tỷ lệ; ...".
    /// </summary>
    public string BufferAllocations { get; private set; } =
        "1024,0.45; 2048,0.25; 4096,0.20; 8192,0.6; 16384,0.4";
}