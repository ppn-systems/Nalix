using Notio.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Notio.Application.Threading;

public class Program
{
    public static void Main(string[] args)
    {
        // Khởi tạo hệ thống logging
        NotioLog.Instance.Initialize();

        Console.WriteLine("Bắt đầu ghi log từ nhiều luồng...");

        // Số lượng luồng muốn tạo
        int threadCount = 100;

        // Sử dụng Task để chạy nhiều luồng
        Task[] tasks = new Task[threadCount];

        for (int i = 0; i < threadCount; i++)
        {
            int threadId = i; // Lưu ID luồng để log
            tasks[i] = Task.Run(() => WriteLogs(threadId));
        }

        // Chờ tất cả các Task hoàn thành
        Task.WaitAll(tasks);

        Console.WriteLine("Hoàn tất ghi log.");
    }

    private static void WriteLogs(int threadId)
    {
        for (int i = 0; i < 10000; i++) // Tạo 1000 log mỗi luồng
        {
            string message = $"[Thread-{threadId}] Log message #{i}";

            // Ghi log với thông tin
            NotioLog.Instance.Info(message);
            NotioLog.Instance.Trace(message);
            NotioLog.Instance.Warn(message);
            NotioLog.Instance.Error(new Exception($"[Thread-{threadId}] Error message #{i}"));

            // Thêm một khoảng dừng nhỏ để mô phỏng tải thực tế
            Thread.Sleep(1);
        }
    }
}