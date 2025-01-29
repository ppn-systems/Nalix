using Notio.Web.Enums;
using Notio.Web.Routing;
using Notio.Web.WebApi;
using System;
using System.Diagnostics;
using System.Net;
using System.Threading.Tasks;

namespace Notio.Application.RestApi;

internal class MainController : WebApiController
{
    [Route(HttpVerbs.Get, "/api/hello")]
    public Task<object> HelloWord()
    {
        // Set status code 200 và response data
        Response.StatusCode = (int)HttpStatusCode.OK;
        return Task.FromResult<object>(new { message = "Hello World!" });
    }

    [Route(HttpVerbs.Get, "/api/greet/{name}")]
    public Task<object> Greet(string name)
    {
        // Set status code 200 và response data
        Response.StatusCode = (int)HttpStatusCode.OK;
        return Task.FromResult<object>(new { message = $"Hello, {name}!" });
    }

    [Route(HttpVerbs.Get, "/api/status")]
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