// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Nalix.Chat.Infrastructure.Hosting;

namespace Nalix.Chat.Infrastructure;

internal static class Program
{
    private static async Task Main(string[] args)
    {
        ChatServerHost server = ChatServerHost.CreateDefault();

        using CancellationTokenSource shutdown = new();

        Console.CancelKeyPress += (_, eventArgs) =>
        {
            eventArgs.Cancel = true;
            shutdown.Cancel();
        };

        await server.RunAsync(shutdown.Token).ConfigureAwait(false);
    }
}
