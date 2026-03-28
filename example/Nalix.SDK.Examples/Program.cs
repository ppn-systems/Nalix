// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.




using Nalix.Common.Diagnostics;
using Nalix.Common.Networking.Packets;
using Nalix.Framework.Configuration;
using Nalix.Framework.DataFrames;
using Nalix.Framework.Injection;
using Nalix.Framework.Random;
using Nalix.Logging;
using Nalix.Logging.Configuration;
using Nalix.Logging.Sinks;
using Nalix.SDK.Examples;
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

        TcpSession client = new();
        await client.ConnectAsync("127.0.0.1", 12345).ConfigureAwait(true);

        _ = await client.PerformAuthenticatedHandshakeAsync(
            clientIdentityProvider: () => "demo-client",
            ed25519KeyProvider: () => (
                PrivateKey: Csprng.GetBytes(32),
                PublicKey: Csprng.GetBytes(32)),
            validateServerPublicKey: serverKey => serverKey.Length == AuthenticatedHandshakeExtensions.X25519PublicKeyLength).ConfigureAwait(true);

        await Task.Delay(TimeSpan.FromSeconds(5)).ConfigureAwait(true);
    }
}
