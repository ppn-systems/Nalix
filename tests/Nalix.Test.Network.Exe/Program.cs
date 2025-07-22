using Nalix.Logging;
using Nalix.Network.Dispatch;
using Nalix.Network.Package;
using Nalix.Shared.Memory.Pooling;
using System;
using System.Threading.Tasks;

namespace Nalix.Test.Network.Exe;
internal class Program
{
    public static async System.Threading.Tasks.Task Main(String[] args)
    {
        ServerListener server = new(
            new ServerProtocol(new PacketDispatchChannel<Packet>(cfg => cfg
                .WithLogging(NLogix.Host.Instance)
                .WithErrorHandling((exception, command) =>
                    NLogix.Host.Instance.Error($"Error handling command: {command}", exception))
            )), new BufferPoolManager(), NLogix.Host.Instance);

        _ = server.StartListeningAsync();           // bắt đầu lắng nghe
        await Task.Delay(10);                         // chạy 10s
        server.StopListening();                       // dừng
        await Task.Delay(5);                          // đợi 5s
        _ = server.StartListeningAsync();           // chạy lại lần nữa
        server.StopListening();                       // dừng tiếp
        _ = server.StartListeningAsync();
        _ = Console.ReadLine();                       // CHỜ người dùng bấm Enter => chương trình không tự thoát

    }
}
