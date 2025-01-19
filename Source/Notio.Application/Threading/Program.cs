using Microsoft.EntityFrameworkCore;
using Notio.Database;
using Notio.Logging;

namespace Notio.Application.Threading;

public class Program
{
    public static void Main()
    {
        // Khởi tạo hệ thống logging
        NotioLog.Instance.Initialize();

        DbContextOptionsBuilder<NotioContext> optionsBuilder = new();
        optionsBuilder.UseSqlite("Data Source=notio.db");

        // Khởi tạo NotioContext với options
        NotioContext dbContext = new(optionsBuilder.Options);

        // Làm việc với dbContext ở đây nếu cần (ví dụ: thao tác với cơ sở dữ liệu)

        // Đảm bảo giải phóng các tài nguyên khi không cần nữa
        dbContext.Dispose();
    }
}