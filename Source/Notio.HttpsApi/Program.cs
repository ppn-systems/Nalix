using Notio.Network.Https.Attributes;
using Notio.Network.Https;
using Notio.Network.Https.Model;
using System;
using System.Threading.Tasks;

namespace Notio.HttpsApi;

class Program
{
    static async Task Main()
    {
        var server = new NotioHttpServer("http://localhost:8080/");

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