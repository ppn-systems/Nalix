using Notio.Network.Web.Http;
using System.Threading.Tasks;

namespace Notio.Network.Web.Request;

/// <summary>
/// A callback used to handle a request.
/// </summary>
/// <param name="context">An <see cref="IHttpContext"/> interface representing the context of the request.</param>
/// <returns>A <see cref="Task"/> representing the ongoing operation.</returns>
public delegate Task RequestHandlerCallback(IHttpContext context);