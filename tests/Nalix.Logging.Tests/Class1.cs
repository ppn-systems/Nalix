using System.Threading;

namespace Nalix.Logging.Tests;

public class Program
{
    public static void Main(System.String[] arg)
    {
        NLogix.Host.Instance.Meta("Hello World!");
        NLogix.Host.Instance.Debug("Hello World!");
        NLogix.Host.Instance.Trace("Hello World!");
        NLogix.Host.Instance.Info("Hello World!");
        NLogix.Host.Instance.Warn("Hello World!");
        NLogix.Host.Instance.Error("Hello World!");
        NLogix.Host.Instance.Fatal("Hello World!");
        NLogix.Host.Instance.Trace("Hello World!");

        Thread.Sleep(5000);
    }
}
