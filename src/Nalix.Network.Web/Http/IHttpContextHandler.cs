using System.Threading.Tasks;

namespace Nalix.Network.Web.Http;

/// <summary>
/// <para>Represents an object that can handle a HTTP context.</para>
/// <para>This API supports the Notio infrastructure and is not intended to be used directly from your code.</para>
/// </summary>
public interface IHttpContextHandler
{
    /// <summary>
    /// <para>Asynchronously handles a HTTP context, generating a suitable response
    /// for an incoming request.</para>
    /// <para>This API supports the Notio infrastructure and is not intended to be used directly from your code.</para>
    /// </summary>
    /// <param name="context">The HTTP context.</param>
    /// <returns>A <see cref="Task"/> representing the ongoing operation.</returns>
    Task HandleContextAsync(IHttpContextImpl context);
}