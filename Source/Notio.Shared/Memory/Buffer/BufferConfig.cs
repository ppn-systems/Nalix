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
    /// Chuỗi phân bổ buffer.
    /// </summary>
    /// <example>
    /// Example:
    /// - "1024,0.40; 2048,0.25; 4096,0.20; 8192,0.60; 16384,0.50; 32768,0.40"
    /// - Các giá trị này biểu thị kích thước của buffer và tỷ lệ phân bổ cho từng kích thước.
    /// </example>
    public string BufferAllocations { get; private set; } = "1024,0.40; 2048,0.25; 4096,0.20; 8192,0.6; 16384,0.5; 32768,0.4";
}