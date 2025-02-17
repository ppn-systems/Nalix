using Notio.Network.Web.Enums;
using Notio.Network.Web.Http;
using Notio.Network.Web.Http.Exceptions;
using Notio.Network.Web.Utilities;
using Notio.Network.Web.WebModule;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Notio.Network.Web.Cors;

/// <summary>
/// Cross-origin resource sharing (CORS) control Module.
/// CORS is a mechanism that allows restricted resources (e.g. fonts)
/// on a web page to be requested from another domain outside the domain from which the resource originated.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="CorsModule" /> class.
/// </remarks>
/// <param name="baseRoute">The base route.</param>
/// <param name="origins">The valid origins. The default is <see cref="All"/> (<c>*</c>).</param>
/// <param name="headers">The valid headers. The default is <see cref="All"/> (<c>*</c>).</param>
/// <param name="methods">The valid methods. The default is <see cref="All"/> (<c>*</c>).</param>
/// <exception cref="ArgumentNullException">
/// origins
/// or
/// headers
/// or
/// methods
/// </exception>
public class CorsModule(
    string baseRoute,
    string origins = CorsModule.All,
    string headers = CorsModule.All,
    string methods = CorsModule.All) : WebModuleBase(baseRoute)
{
    /// <summary>
    /// A string meaning "All" in CORS headers.
    /// </summary>
    public const string All = "*";

    private readonly string _origins = origins ?? throw new ArgumentNullException(nameof(origins));
    private readonly string _headers = headers ?? throw new ArgumentNullException(nameof(headers));
    private readonly string _methods = methods ?? throw new ArgumentNullException(nameof(methods));

    private readonly string[] _validOrigins =
            [.. origins.ToLowerInvariant()
                .SplitByComma(StringSplitOptions.RemoveEmptyEntries)
                .Select(x => x.Trim())];

    private readonly string[] _validMethods =
            [.. methods.ToLowerInvariant()
                .SplitByComma(StringSplitOptions.RemoveEmptyEntries)
                .Select(x => x.Trim())];

    /// <inheritdoc />
    public override bool IsFinalHandler => false;

    /// <inheritdoc />
    protected override Task OnRequestAsync(IHttpContext context)
    {
        bool isOptions = context.Request.HttpVerb == HttpVerbs.Options;

        // If we allow all we don't need to filter
        if (_origins == All && _headers == All && _methods == All)
        {
            context.Response.Headers.Set(HttpHeaderNames.AccessControlAllowOrigin, All);

            if (isOptions)
            {
                ValidateHttpOptions(context);
                context.SetHandled();
            }

            return Task.CompletedTask;
        }

        string? currentOrigin = context.Request.Headers[HttpHeaderNames.Origin];

        if (string.IsNullOrWhiteSpace(currentOrigin) && context.Request.IsLocal)
        {
            return Task.CompletedTask;
        }

        if (_origins == All)
        {
            return Task.CompletedTask;
        }

        if (_validOrigins.Contains(currentOrigin))
        {
            context.Response.Headers.Set(HttpHeaderNames.AccessControlAllowOrigin, currentOrigin);

            if (isOptions)
            {
                ValidateHttpOptions(context);
                context.SetHandled();
            }
        }

        return Task.CompletedTask;
    }

    private void ValidateHttpOptions(IHttpContext context)
    {
        string? requestHeadersHeader = context.Request.Headers[HttpHeaderNames.AccessControlRequestHeaders];
        if (!string.IsNullOrWhiteSpace(requestHeadersHeader))
        {
            // TODO: Remove unwanted headers from request
            context.Response.Headers.Set(HttpHeaderNames.AccessControlAllowHeaders, requestHeadersHeader);
        }

        string? requestMethodHeader = context.Request.Headers[HttpHeaderNames.AccessControlRequestMethod];
        if (string.IsNullOrWhiteSpace(requestMethodHeader))
        {
            return;
        }

        System.Collections.Generic.IEnumerable<string> currentMethods = requestMethodHeader.ToLowerInvariant()
            .SplitByComma(StringSplitOptions.RemoveEmptyEntries)
            .Select(x => x.Trim());

        if (_methods != All && !currentMethods.Any(_validMethods.Contains))
        {
            throw HttpException.BadRequest();
        }

        context.Response.Headers.Set(HttpHeaderNames.AccessControlAllowMethods, requestMethodHeader);
    }
}
