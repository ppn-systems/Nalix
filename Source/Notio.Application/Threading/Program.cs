using Notio.Logging;

namespace Notio.Application.Threading;

public class Program
{
    public static void Main(string[] args)
    {
        // Khởi tạo hệ thống logging
        NotioLog.Instance.Initialize();

        NotioLog.Instance.Info("aaaa");
    }
}