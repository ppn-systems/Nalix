using Nalix.Logging.Interop;

namespace Nalix.Logging.Tests;

public static class Program
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Interoperability", "CA1416:Validate platform compatibility", Justification = "<Pending>")]
    public static void Main(System.String[] args)
    {
        NLogix.Host.Instance.Error("\x1b[38;5;214mThis is Orange 214\x1b[0m");
        NLogix.Host.Instance.Info("Hello, Nalix.Logging.Tests!");

        System.Threading.Thread.Sleep(1000);

        using (new TransientConsoleScope("Nalix Report", cols: 120, rows: 35))
        {
            // Từ đây trở đi Console.Write* sẽ in vào console mới
            TransientConsoleScope.WriteLine("=== Build Summary ===");
            TransientConsoleScope.WriteLine($"Time : {System.DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            TransientConsoleScope.WriteLine($"User : {System.Environment.UserName}");
            TransientConsoleScope.WriteLine(new System.String('-', 40));
            TransientConsoleScope.WriteLine("• Pass: 128 tests");
            TransientConsoleScope.WriteLine("• Fail: 0");
            TransientConsoleScope.WriteLine("• Warnings: 3");
            TransientConsoleScope.WriteLine("\x1b[32mSUCCESS\x1b[0m");
            TransientConsoleScope.WriteLine("");
            TransientConsoleScope.ReadKey();
        }

        NLogix.Host.Instance.Error("\x1b[38;5;214mThis is Orange 214\x1b[0m");

        System.Console.ReadKey(true);
    }
}
