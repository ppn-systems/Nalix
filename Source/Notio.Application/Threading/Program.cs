using Microsoft.EntityFrameworkCore;
using Notio.Application.Main;
using Notio.Database;
using Notio.Http;
using Notio.Logging;
using System.Threading.Tasks;

namespace Notio.Application.Threading;

public static class Program
{
    public static async Task Main()
    {
        // Khởi tạo hệ thống logging
        NotioLog.Instance.Initialize();

        DbContextOptionsBuilder<NotioContext> optionsBuilder = new();
        optionsBuilder.UseSqlite("Data Source=notio.db");

        // Khởi tạo NotioContext với options
        NotioContext dbContext = new(optionsBuilder.Options);

        // Làm việc với dbContext ở đây nếu cần (ví dụ: thao tác với cơ sở dữ liệu)
        HttpServer httpServer = new("http://localhost:5000/");

        httpServer.RegisterController<WebApi>();

        await httpServer.StartAsync();

        System.Console.ReadKey();

        // Đảm bảo giải phóng các tài nguyên khi không cần nữa
        dbContext.Dispose();
    }
}