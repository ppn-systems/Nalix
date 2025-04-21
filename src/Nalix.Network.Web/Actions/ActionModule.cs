using Nalix.Network.Web.Enums;
using Nalix.Network.Web.Http;
using Nalix.Network.Web.Http.Request;
using Nalix.Network.Web.Utilities;
using Nalix.Network.Web.WebModule;
using System;
using System.Threading.Tasks;

namespace Nalix.Network.Web.Actions;

/// <summary>
/// A module that passes requests to a callback.
/// </summary>
/// <seealso cref="WebModuleBase" />
/// <remarks>
/// Initializes a new instance of the <see cref="ActionModule" /> class.
/// </remarks>
/// <param name="baseRoute">The base route.</param>
/// <param name="verb">The HTTP verb that will be served by this module.</param>
/// <param name="handler">The callback used to handle requests.</param>
/// <exception cref="ArgumentNullException"><paramref name="handler"/> is <see langword="null"/>.</exception>
/// <seealso cref="WebModuleBase(string)"/>
public class ActionModule(string baseRoute, HttpVerbs verb, RequestHandlerCallback handler) : WebModuleBase(baseRoute)
{
    private readonly HttpVerbs _verb = verb;

    private readonly RequestHandlerCallback _handler = Validate.NotNull(nameof(handler), handler);

    /// <summary>
    /// Initializes a new instance of the <see cref="ActionModule"/> class.
    /// </summary>
    /// <param name="handler">The handler.</param>
    public ActionModule(RequestHandlerCallback handler)
        : this("/", HttpVerbs.Any, handler)
    {
    }

    /// <inheritdoc />
    public override bool IsFinalHandler => false;

    /// <inheritdoc />
    protected override async Task OnRequestAsync(IHttpContext context)
    {
        if (_verb != HttpVerbs.Any && context.Request.HttpVerb != _verb)
        {
            return;
        }

        await _handler(context).ConfigureAwait(false);
        context.SetHandled();
    }
}