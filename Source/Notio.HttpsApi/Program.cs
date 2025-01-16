using Notio.Network.Http.Attributes;
using Notio.Network.Http;
using Notio.Network.Http.Model;
using System;
using System.Threading.Tasks;

namespace Notio.HttpsApi;

class Program
{
    static async Task Main()
    {
        var server = new HttpServer("http://localhost:8080/");

        // Đăng ký middleware
        server.UseMiddleware<LoggingMiddleware>();

        // Đăng ký controller
        server.RegisterController<UserController>();

        // Start server
        await server.StartAsync();

        Console.WriteLine("Press any key to stop...");
        Console.ReadKey();

        server.Stop();
    }
}