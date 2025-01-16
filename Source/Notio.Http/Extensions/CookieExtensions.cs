using System.Linq;
using Notio.Http.Utils;
using Notio.Http.Cookie;
using Notio.Http.Extensions;
using Notio.Http.Interfaces;

namespace Notio.Http.Extensions;

/// <summary>
/// Fluent extension methods for working with HTTP cookies.
/// </summary>
public static class CookieExtensions
{
    /// <summary>
    /// Adds or updates a name-value pair in this request's Cookie header.
    /// To automatically maintain a cookie "session", consider using a CookieJar or CookieSession instead.
    /// </summary>
    /// <param name="request">The IRequest.</param>
    /// <param name="name">The cookie name.</param>
    /// <param name="value">The cookie value.</param>
    /// <returns>This INotioClient instance.</returns>
    public static IRequest WithCookie(this IRequest request, string name, object value)
    {
        var cookies = new NameValueList<string>(request.Cookies, true); // cookie names are case-sensitive https://stackoverflow.com/a/11312272/62600
        cookies.AddOrReplace(name, value.ToInvariantString());
        return request.WithHeader("Cookie", CookieCutter.BuildRequestHeader(cookies));
    }

    /// <summary>
    /// Adds or updates name-value pairs in this request's Cookie header, based on property names/values
    /// of the provided object, or keys/values if object is a dictionary.
    /// To automatically maintain a cookie "session", consider using a CookieJar or CookieSession instead.
    /// </summary>
    /// <param name="request">The IRequest.</param>
    /// <param name="values">Names/values of HTTP cookies to set. Typically an anonymous object or IDictionary.</param>
    /// <returns>This INotioClient.</returns>
    public static IRequest WithCookies(this IRequest request, object values)
    {
        var cookies = new NameValueList<string>(request.Cookies, true); // cookie names are case-sensitive https://stackoverflow.com/a/11312272/62600
                                                                        // although rare, we need to accommodate the possibility of multiple cookies with the same name
        foreach (var group in values.ToKeyValuePairs().GroupBy(x => x.Key))
        {
            // add or replace the first one (by name)
            cookies.AddOrReplace(group.Key, group.First().Value.ToInvariantString());
            // append the rest
            foreach (var (Key, Value) in group.Skip(1))
                cookies.Add(Key, Value.ToInvariantString());
        }
        return request.WithHeader("Cookie", CookieCutter.BuildRequestHeader(cookies));
    }

    /// <summary>
    /// Sets the CookieJar associated with this request, which will be updated with any Set-Cookie headers present
    /// in the response and is suitable for reuse in subsequent requests.
    /// </summary>
    /// <param name="request">The IRequest.</param>
    /// <param name="cookieJar">The CookieJar.</param>
    /// <returns>This INotioClient instance.</returns>
    public static IRequest WithCookies(this IRequest request, CookieJar cookieJar)
    {
        request.CookieJar = cookieJar;
        return request;
    }

    /// <summary>
    /// Creates a new CookieJar and associates it with this request, which will be updated with any Set-Cookie
    /// headers present in the response and is suitable for reuse in subsequent requests.
    /// </summary>
    /// <param name="request">The IRequest.</param>
    /// <param name="cookieJar">The created CookieJar, which can be reused in subsequent requests.</param>
    /// <returns>This INotioClient instance.</returns>
    public static IRequest WithCookies(this IRequest request, out CookieJar cookieJar)
    {
        cookieJar = new CookieJar();
        return request.WithCookies(cookieJar);
    }
}
