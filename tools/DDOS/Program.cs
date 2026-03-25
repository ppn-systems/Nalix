// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.
using Nalix.Common.Messaging.Packets.Abstractions;
using Nalix.Framework.Injection;
using Nalix.Shared.Messaging.Catalog;

namespace DDoS;

internal static class Program
{
    /// <summary>
    ///  The main entry point for the application.
    /// </summary>
    [System.STAThread]
    private static void Main()
    {
        // 1) Build packet catalog.
        PacketCatalogFactory factory = new();
        IPacketCatalog catalog = factory.CreateCatalog();

        // 2) Expose catalog through your current service locator.
        InstanceManager.Instance.Register<IPacketCatalog>(catalog);
        // To customize application configuration such as set high DPI settings or default font,
        // see https://aka.ms/applicationconfiguration.
        ApplicationConfiguration.Initialize();
        Application.Run(new Form1());
    }
}