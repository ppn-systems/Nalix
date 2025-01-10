using Notio.Shared.Configuration;

namespace Notio.Shared.Memory.Buffer;

/// <summary>
/// Configuration for buffer settings.
/// </summary>
public sealed class BufferConfig : ConfigurationBinder
{
    /// <summary>
    /// Tổng số lượng buffers được tạo.
    /// </summary>
    public int TotalBuffers { get; set; } = 100;

    /// <summary>
    /// Chuỗi phân bổ buffer.
    /// </summary>
    /// <example>
    /// Example:
    /// - "1024,0.40; 2048,0.25; 4096,0.20; 8192,0.60; 16384,0.50; 32768,0.40"
    /// - Các giá trị này biểu thị kích thước của buffer và tỷ lệ phân bổ cho từng kích thước.
    /// </example>
    public string BufferAllocations { get; set; } =
        "256,0.05; 512,0.10; 1024,0.25; " +
        "2048,0.20; 4096,0.15; 8192,0.10; " +
        "16384,0.10; 32768,0.03; 65536,0.02";
}