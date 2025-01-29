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
    //
    [Route(HttpVerbs.Get, "/")]
    public Task<object> Notio()
    {
        // Set status code 200 and response data
        Response.StatusCode = (int)HttpStatusCode.OK;
        return Task.FromResult<object>(new { message = "Notio-Api" });
    }

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

    [Route(HttpVerbs.Get, "/server-time")]
    public Task<object> GetServerTime()
    {
        Response.StatusCode = (int)HttpStatusCode.OK;
        return Task.FromResult<object>(new { serverTime = DateTime.Now });
    }

    [Route(HttpVerbs.Get, "/health-check")]
    public Task<object> HealthCheck()
    {
        Response.StatusCode = (int)HttpStatusCode.OK;
        return Task.FromResult<object>(new { status = "Healthy" });
    }
}