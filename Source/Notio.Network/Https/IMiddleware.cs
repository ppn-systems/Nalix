using Notio.Network.Https.Model;
using System.Threading.Tasks;

namespace Notio.Network.Https
{
    public interface IMiddleware
    {
        Task InvokeAsync(NotioHttpsContext context);
    }
}