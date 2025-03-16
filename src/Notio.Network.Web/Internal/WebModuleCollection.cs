using Notio.Network.Web.Http;
using Notio.Network.Web.Http.Extensions;
using Notio.Network.Web.Utilities;
using Notio.Network.Web.WebModule;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace Notio.Network.Web.Internal;

internal sealed class WebModuleCollection : DisposableComponentCollection<IWebModule>
{
    private readonly string _logSource;

    internal WebModuleCollection(string logSource)
        => _logSource = logSource;

    internal void StartAll(CancellationToken cancellationToken)
    {
        foreach ((string name, IWebModule module) in WithSafeNames)
        {
            Trace.WriteLine($"Starting module {name}...", _logSource);
            module.Start(cancellationToken);
        }
    }

    internal async Task DispatchRequestAsync(IHttpContext context)
    {
        if (context.IsHandled)
        {
            return;
        }

        string requestedPath = context.RequestedPath;
        foreach ((string name, IWebModule module) in WithSafeNames)
        {
            Routing.RouteMatch routeMatch = module.MatchUrlPath(requestedPath);
            if (routeMatch == null)
            {
                continue;
            }

            Trace.WriteLine($"[{context.Id}] Processing with {name}.", _logSource);
            context.GetImplementation().Route = routeMatch;
            await module.HandleRequestAsync(context).ConfigureAwait(false);
            if (context.IsHandled)
            {
                break;
            }
        }
    }
}
