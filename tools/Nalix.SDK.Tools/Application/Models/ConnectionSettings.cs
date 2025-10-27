// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System.Net;

namespace Application.Models;

/// <summary>
/// Kết nối server/client dùng trong MVVM. Có xác thực input, bảo mật, dễ debug.
/// </summary>
public class ConnectionSettings
{
    /// <summary>
    /// Địa chỉ host server.
    /// </summary>
    public String Host { get; set; } = String.Empty;

    /// <summary>
    /// Cổng kết nối.
    /// </summary>
    public Int32 Port { get; set; }

    /// <summary>
    /// Kiểm tra input có hợp lệ không (input validation).
    /// </summary>
    /// <param name="error">Thông báo lỗi (nếu có).</param>
    /// <returns>True nếu hợp lệ, False nếu có lỗi.</returns>
    public Boolean IsValid(out String error)
    {
        error = "";

        // Kiểm tra host không được trống, độ dài tối đa 255, không chứa ký tự nguy hiểm
        if (String.IsNullOrWhiteSpace(Host) || Host.Length > 255)
        {
            error = "Host không hợp lệ (Host must be non-empty, length <= 255)";
            return false;
        }
        // Kiểm tra định dạng host hợp lệ (IP hoặc domain)
        if (!IsHostValid(Host))
        {
            error = "Host phải là IP hợp lệ hoặc domain (Host must be valid IP or domain)";
            return false;
        }
        // Cổng phải trong khoảng hợp lệ
        if (Port is < 1 or > 65535)
        {
            error = "Port phải từ 1 đến 65535 (Port must be in range 1-65535)";
            return false;
        }
        return true;
    }

    /// <summary>
    /// Kiểm tra tính hợp lệ của chuỗi host (bảo mật tránh trường hợp lạ).
    /// </summary>
    private static Boolean IsHostValid(String host)
    {
        // Kiểm tra IP
        if (IPAddress.TryParse(host, out _))
        {
            return true;
        }

        // Kiểm tra theo domain pattern bình thường
        var domainOk = Uri.CheckHostName(host) != UriHostNameType.Unknown;
        return domainOk;
    }
}
