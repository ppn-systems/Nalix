// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.
// Copyright (c) 2026 PPN Corporation. All rights reserved.

namespace DDoS.Models;

/// <summary>
/// Lưu trữ thông tin về packet đã gửi để hiển thị trong history
/// </summary>
public class PacketHistory
{
    /// <summary>
    /// Thời gian gửi packet
    /// </summary>
    public DateTime Timestamp { get; set; }

    /// <summary>
    /// Loại packet
    /// </summary>
    public PacketType Type { get; set; }

    /// <summary>
    /// Nội dung packet (được format cho dễ đọc)
    /// </summary>
    public String Content { get; set; } = String.Empty;

    /// <summary>
    /// Kích thước packet (bytes)
    /// </summary>
    public Int32 Size { get; set; }

    /// <summary>
    /// Trạng thái gửi (thành công hay thất bại)
    /// </summary>
    public Boolean Success { get; set; }

    /// <summary>
    /// Thông báo lỗi (nếu có)
    /// </summary>
    public String? ErrorMessage { get; set; }

    /// <summary>
    /// Format hiển thị cho ListBox
    /// </summary>
    public override String ToString()
    {
        String status = Success ? "✓" : "✗";
        String error = String.IsNullOrEmpty(ErrorMessage) ? "" : $" - {ErrorMessage}";
        return $"[{Timestamp:HH:mm:ss}] {status} {Type} ({Size}B): {Content}{error}";
    }
}