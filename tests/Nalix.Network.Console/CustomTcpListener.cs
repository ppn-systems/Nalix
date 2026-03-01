using Nalix.Network.Abstractions;
using Nalix.Network.Listeners.Tcp;
using System;

public sealed class CustomTcpListener : TcpListenerBase
{
    public CustomTcpListener(UInt16 port, IProtocol protocol)
        : base(port, protocol)
    {
    }

    /// <summary>
    /// Xử lý khi nhận được dữ liệu từ client.
    /// </summary>
    public override void SynchronizeTime(Int64 milliseconds)
    {
        // Ở đây không cần đồng bộ thời gian, ta có thể để trống.
    }
}