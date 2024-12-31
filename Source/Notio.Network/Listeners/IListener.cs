using System.Threading;

namespace Notio.Network.Listeners;

internal interface IListener
{
    void BeginListening(CancellationToken cancellationToken);

    void EndListening();
}