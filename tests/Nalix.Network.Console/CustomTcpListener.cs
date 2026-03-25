// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.
using Nalix.Common.Networking;
using Nalix.Network.Listeners.Tcp;

public sealed class CustomTcpListener(ushort port, IProtocol protocol) : TcpListenerBase(port, protocol)
{

    /// <summary>
    /// Xử lý khi nhận được dữ liệu từ client.
    /// </summary>
    public override void SynchronizeTime(long milliseconds)
    {
        // Ở đây không cần đồng bộ thời gian, ta có thể để trống.
    }
}