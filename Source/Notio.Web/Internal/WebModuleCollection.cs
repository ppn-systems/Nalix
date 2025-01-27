using Notio.Web.Utilities;
using Swan.Logging;
using System.Threading;
using System.Threading.Tasks;

namespace Notio.Web.Internal
{
    internal sealed class WebModuleCollection : DisposableComponentCollection<IWebModule>
    {
        private readonly string _logSource;

        internal WebModuleCollection(string logSource)
        {
            _logSource = logSource;
        }

        internal void StartAll(CancellationToken cancellationToken)
        {
            foreach (var (name, module) in WithSafeNames)
            {
                $"Starting module {name}...".Debug(_logSource);
                module.Start(cancellationToken);
            }
        }

        internal async Task DispatchRequestAsync(IHttpContext context)
        {
            if (context.IsHandled)
                return;

            var requestedPath = context.RequestedPath;
            foreach (var (name, module) in WithSafeNames)
            {
                var routeMatch = module.MatchUrlPath(requestedPath);
                if (routeMatch == null)
                    continue;

                $"[{context.Id}] Processing with {name}.".Debug(_logSource);
                context.GetImplementation().Route = routeMatch;
                await module.HandleRequestAsync(context).ConfigureAwait(false);
                if (context.IsHandled)
                    break;
            }
        }
    }
}