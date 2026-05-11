using Nalix.Framework.Memory.Objects;
using Nalix.Network.Connections;

namespace Nalix.Debug;

public static class Program
{
    public static void Main(string[] args)
    {
        var mgr = new ObjectPoolManager();
        var pool = mgr.GetTypedPool<ConnectionEventArgs>();

        // Lấy 10 cái
        var list = pool.GetMultiple(100);
        Console.WriteLine($"After Get 100 → Available: {mgr.GetTypeInfo<ConnectionEventArgs>()["AvailableCount"]}");

        // Dùng xong, return hết
        pool.ReturnMultiple(list);
        Console.WriteLine($"After Return 100 → Available: {mgr.GetTypeInfo<ConnectionEventArgs>()["AvailableCount"]}");
    }
}
