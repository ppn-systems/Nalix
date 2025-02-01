using Notio.Network.Web.Enums;
using Notio.Network.Web.Routing;
using Notio.Network.Web.WebApi;
using System;
using System.Diagnostics;
using System.Net;
using System.Threading.Tasks;

namespace Notio.Application.RestApi;

internal class MainController : WebApiController
{
    [Route(HttpVerbs.Get, "/")]
    [Route(HttpVerbs.Get, "/hello")]
    public Task<object> HelloWord()
    {
        // Set status code 200 and response data
        Response.StatusCode = (int)HttpStatusCode.OK;
        return Task.FromResult<object>(new { message = "Hello World!" });
    }

    [Route(HttpVerbs.Get, "/status")]
    public Task<object> Status()
    {
        Response.StatusCode = (int)HttpStatusCode.OK;
        return Task.FromResult<object>(new
        {
            message = "Server is running",
            OS = Environment.OSVersion.ToString(),
            Framework = Environment.Version.ToString(),
            Environment.MachineName,
            Uptime = $"{DateTime.Now - Process.GetCurrentProcess().StartTime:hh\\:mm\\:ss}",
            Timestamp = DateTime.UtcNow
        });
    }
}