// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Nalix.Framework.Configuration;
using Nalix.Framework.DataFrames;
using Nalix.Framework.DataFrames.SignalFrames;
using Nalix.Framework.Random;
using Nalix.Logging.Options;
using Nalix.SDK.Transport;

internal class Program
{
    private static async Task Main(string[] args)
    {
        ConfigurationManager.Instance.Get<NLogixOptions>()
                            .MinLevel = Microsoft.Extensions.Logging.LogLevel.Debug;
        PacketRegistry packetRegistry = new PacketRegistryFactory().CreateCatalog();

        TcpSession client = new(new Nalix.SDK.Options.TransportOptions(), packetRegistry);
        await client.ConnectAsync("127.0.0.1", 12345).ConfigureAwait(true);

        Handshake hand = new()
        {
            Data = Csprng.GetBytes(30002)
        };
        byte[] bytes = hand.Serialize();

        for (int i = 0; i < 1000; i++)
        {
            await client.SendAsync(bytes).ConfigureAwait(false);
        }


        await Task.Delay(TimeSpan.FromSeconds(5)).ConfigureAwait(true);
    }
}
