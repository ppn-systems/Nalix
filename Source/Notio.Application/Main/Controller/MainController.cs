using Notio.Network.Http;
using Notio.Network.Http.Attributes;
using System;
using System.Diagnostics;
using System.Net;
using System.Threading.Tasks;

namespace Notio.Application.Main.Controller;

[ApiController]
internal class MainController
{
    // Endpoint chào mừng đơn giản
    [Route("/api", HttpMethodType.GET)]
    public static async Task HelloWorld(HttpContext context)
        => await context.Response.WriteJsonResponseAsync(
            HttpStatusCode.OK,
            new
            {
                Message = "Hello, World!"
            }
        );

    // Endpoint trả về trạng thái server
    [Route("/api/status", HttpMethodType.GET)]
    public static async Task ServerStatus(HttpContext context)
        => await context.Response.WriteJsonResponseAsync(
            HttpStatusCode.OK,
            new
            {
                Status = "Running",
                Timestamp = DateTime.UtcNow
            }
        );

    // Endpoint xử lý lỗi demo
    [Route("/api/error", HttpMethodType.GET)]
    public static async Task SimulateError(HttpContext context)
    {
        await context.Response.WriteErrorResponseAsync(
            HttpStatusCode.InternalServerError,
            "This is a simulated error for testing purposes."
        );
    }

    // Endpoint trả về thông tin hệ thống
    [Route("/api/system-info", HttpMethodType.GET)]
    public static async Task SystemInfo(HttpContext context)
        => await context.Response.WriteJsonResponseAsync(
            HttpStatusCode.OK,
            new
            {
                OS = Environment.OSVersion.ToString(),
                Framework = Environment.Version.ToString(),
                Environment.MachineName,
                Uptime = $"{(DateTime.Now - Process.GetCurrentProcess().StartTime):hh\\:mm\\:ss}"
            }
        );
}