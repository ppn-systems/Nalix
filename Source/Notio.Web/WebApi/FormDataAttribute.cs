using Notio.Web.Http;
using System;
using System.Collections.Specialized;
using System.Threading.Tasks;

namespace Notio.Web.WebApi;

/// <summary>
/// <para>Specifies that a parameter of a controller method will receive a <see cref="NameValueCollection"/>
/// of HTML form data, obtained by deserializing a request body with a content type
/// of <c>application/x-www-form-urlencoded</c>.</para>
/// <para>The received collection will be read-only.</para>
/// <para>This class cannot be inherited.</para>
/// </summary>
/// <seealso cref="Attribute" />
/// <seealso cref="IRequestDataAttribute{TController,TData}" />
[AttributeUsage(AttributeTargets.Parameter)]
public sealed class FormDataAttribute : Attribute, IRequestDataAttribute<WebApiController, NameValueCollection>
{
    /// <inheritdoc />
    public async Task<NameValueCollection?> GetRequestDataAsync(WebApiController controller, string parameterName)
    {
        return await controller.HttpContext.GetRequestFormDataAsync();
    }
}