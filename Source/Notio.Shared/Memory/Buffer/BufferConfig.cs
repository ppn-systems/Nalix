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
        "256,0.40; 512,0.25; 1024,0.15; 2048,0.10; 4096,0.05; 8192,0.03; 16384,0.02";
}