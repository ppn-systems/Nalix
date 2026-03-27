// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.




using Nalix.Common.Diagnostics;
using Nalix.Common.Networking.Packets;
using Nalix.Framework.Configuration;
using Nalix.Framework.DataFrames;
using Nalix.Framework.DataFrames.SignalFrames;
using Nalix.Framework.Injection;
using Nalix.Framework.Random;
using Nalix.Logging;
using Nalix.Logging.Configuration;
using Nalix.Logging.Sinks;
using Nalix.SDK.Transport;

internal class Program
{
    private static async Task Main(string[] args)
    {
        ConfigurationManager.Instance.Get<NLogixOptions>()
                            .MinLevel = LogLevel.Debug;

        ILogger logger = new NLogix(cfg => cfg.RegisterTarget(new BatchConsoleLogTarget(t => t.EnableColors = true)));
        InstanceManager.Instance.Register(logger);
        PacketRegistry packetRegistry = new PacketRegistryFactory().CreateCatalog();
        InstanceManager.Instance.Register<IPacketRegistry>(packetRegistry);

        //TcpSession client = InstanceManager.Instance.GetOrCreateInstance<TcpSession>();

        Handshake handshake = new(0, Csprng.GetBytes(3000002));

        TcpSession client = new();
        await client.ConnectAsync("127.0.0.1", 12345);
        Console.WriteLine(handshake.GenerateReport());
        Console.WriteLine($"Handshake Magic: {handshake.MagicNumber:X8}");
        byte[] data = handshake.Serialize();
        _ = await client.SendAsync(data);
        Console.WriteLine($"Handshake Length: {data.Length}");

        //for (Int32 i = 0; i < 100000; i++)
        //{
        //    System.Console.Write($"Sending handshake packet {i + 1}/100000...");
        //    await client.SendAsync(data);
        //}

        await Task.Delay(10000000); // Wait for response (for demonstration purposes)
    }
}
