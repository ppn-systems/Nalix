using Nalix.Framework.Tasks;
using System.Threading;

namespace Nalix.Logging.Tests;

public class Program
{
    public static void Main(System.String[] arg)
    {
        NLogix.Host.Instance.Info(new TaskManager().GenerateReport());

        Thread.Sleep(5000);
    }
}
