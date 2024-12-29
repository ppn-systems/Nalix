using Notio.Common.Contracts.Network;
using Notio.Common.IMemory;
using Notio.Infrastructure.Time;
using Notio.Network.IO;
using Notio.Security;
using System;
using System.IO;
using System.Net.Sockets;

namespace Notio.Network.Connection;

public class Connection : IConnection
{
    private readonly Socket _socket;
    private readonly SocketWriter _socketWriter;
    private readonly SocketReader _socketReader;

    public Connection(Socket socket, IBufferAllocator bufferAllocator)
    {
        _socket = socket;
        _socketWriter = new SocketWriter(_socket, bufferAllocator);
        _socketReader = new SocketReader(_socket, bufferAllocator);

        AesKey = Aes256.GenerateKey();
        IsAuthenticated = false;
        LastPingResponse = Clock.UnixTicksNow;
        Ip = _socket.RemoteEndPoint?.ToString() ?? string.Empty;
        TimeStamp = Clock.UnixSecondsNow;
    }

    public string Ip { get; private set; }
    public byte[] AesKey { get; private set; }
    public long TimeStamp { get; private set; }
    public bool IsAuthenticated { get; private set; }
    public bool Disconnected { get; private set; }

    public long LastPingRequest { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
    public long LastPingResponse { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

    public event EventHandler<IConnectionEventArgs>? OnProcessEvent;
    public event EventHandler<IConnectionEventArgs>? OnCloseEvent;
    public event EventHandler<IConnectionEventArgs>? OnPostProcessEvent;

    public void BeginStreamRead()
    {
    }

    public void Close(bool force = false)
    {
        throw new NotImplementedException();
    }

    public void Disconnect(string text = null)
    {
        throw new NotImplementedException();
    }

    public void Send(byte[] data)
    {
        throw new NotImplementedException();
    }

    public void SendFirstConnection()
    {
        throw new NotImplementedException();
    }

    public void SetAsAuthenticated()
    {
        throw new NotImplementedException();
    }

    public void SetAes(byte[] key)
    {
        throw new NotImplementedException();
    }
}
