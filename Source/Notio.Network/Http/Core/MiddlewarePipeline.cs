using System.Collections.Generic;
using System.Threading.Tasks;

namespace Notio.Network.Http.Core;

public class MiddlewarePipeline
{
    private readonly List<MiddlewareBase> _middlewares = [];

    public void AddMiddleware(MiddlewareBase middleware) => _middlewares.Add(middleware);

    public async Task ExecuteAsync(HttpContext context)
    {
        if (_middlewares.Count > 0)
        {
            MiddlewareBase first = _middlewares[0];
            for (int i = 0; i < _middlewares.Count - 1; i++)
            {
                _middlewares[i].SetNext(_middlewares[i + 1]);
            }
            await first.InvokeAsync(context);
        }
    }

    public async Task ShutdownAsync()
    {
    }
}