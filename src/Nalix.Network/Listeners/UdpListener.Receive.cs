using Nalix.Common.Connection;
using Nalix.Common.Security.Identity;
using Nalix.Framework.Identity;
using Nalix.Network.Connection;

namespace Nalix.Network.Listeners.Udp;

public abstract partial class UdpListenerBase
{
    private async System.Threading.Tasks.Task ReceiveDatagramsAsync(
        System.Threading.CancellationToken cancellationToken)
    {
        System.ArgumentNullException.ThrowIfNull(this._udpClient);
        System.ObjectDisposedException.ThrowIf(this._isDisposed, this);

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                System.Net.Sockets.UdpReceiveResult result = await this._udpClient
                    .ReceiveAsync(cancellationToken)
                    .ConfigureAwait(false);

                _ = System.Threading.ThreadPool.UnsafeQueueUserWorkItem(
                    static state =>
                    {
                        CallbackState s = (CallbackState)state!;
                        s.Listener.ProcessDatagram(s.Result);
                    },
                    new CallbackState { Listener = this, Result = result });
            }
            catch (System.OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (System.Exception ex) when (!cancellationToken.IsCancellationRequested)
            {
                this._logger.Error("[UDP] Receive error on {0}: {1}", Config.Port, ex.Message);
                await System.Threading.Tasks.Task.Delay(50, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private void ProcessDatagram(System.Net.Sockets.UdpReceiveResult result)
    {
        if (!this.IsAuthenticated(result))
        {
            this._logger.Warn($"[UDP] Unauthenticated packet from {result.RemoteEndPoint}");
            return;
        }

        IIdentifier identifier = Identifier.Deserialize(result.Buffer[^7..]);
        IConnection? connection = ConnectionHub.Instance.GetConnection(identifier);
        ((Connection.Connection?)connection)?.InjectIncoming(result.Buffer);
    }

    private struct CallbackState
    {
        public required UdpListenerBase Listener;
        public required System.Net.Sockets.UdpReceiveResult Result;
    }
}

