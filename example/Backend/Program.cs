using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Logging;
using Nalix.Framework.Injection;
using Nalix.Hosting;

namespace Backend;

[DebuggerStepThrough]
[ExcludeFromCodeCoverage]
public static class Program
{
    private static readonly Action<ILogger, string, ushort, Exception?> s_listeningMessage =
        LoggerMessage.Define<string, ushort>(LogLevel.Information, new EventId(1000, nameof(Program)), "server-listening tcp://{Address}:{Port}");

    [STAThread]
    [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Top-level entry point logs fatal failures before exiting.")]
    [SuppressMessage("Reliability", "CA2007:Consider calling ConfigureAwait on the awaited task", Justification = "Async disposal belongs to the console entry point lifecycle.")]
    public static async Task<int> Main()
    {
        ILogger logger = Startup.CreateBootstrapLogger();
        using CancellationTokenSource exit = new();

        Console.CursorVisible = false;
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            exit.Cancel();
        };

        await using NetworkApplication host = Startup.Configure(logger);

        try
        {
            WriteControls(logger);
            await RunCommandLoopAsync(host, logger, exit.Token).ConfigureAwait(false);

            return 0;
        }
        catch (OperationCanceledException)
        {
            return 0;
        }
        catch (Exception ex)
        {
            ILogger activeLogger = InstanceManager.Instance.GetExistingInstance<ILogger>() ?? logger;
            activeLogger.LogError(ex, "server-fatal");

            return -1;
        }
        finally
        {
            await host.DeactivateAsync().ConfigureAwait(false);
            Console.CursorVisible = true;
        }
    }

    private static async Task RunCommandLoopAsync(
        NetworkApplication host,
        ILogger logger,
        CancellationToken exitToken)
    {
        while (!exitToken.IsCancellationRequested)
        {
            ConsoleKeyInfo key = await ReadKeyAsync(exitToken).ConfigureAwait(false);

            if (key.Modifiers.HasFlag(ConsoleModifiers.Control) && key.Key == ConsoleKey.R)
            {
                await host.ActivateAsync(exitToken).ConfigureAwait(false);
                s_listeningMessage(logger, Startup.ListenAddress, Startup.ListenPort, null);
                continue;
            }

            if (key.Modifiers.HasFlag(ConsoleModifiers.Control) && key.Key == ConsoleKey.S)
            {
                await host.DeactivateAsync(CancellationToken.None).ConfigureAwait(false);
                logger.LogInformation("server-stopped");
                continue;
            }

            if (key.Modifiers.HasFlag(ConsoleModifiers.Control) && key.Key == ConsoleKey.C)
            {
                logger.LogInformation("server-exit");
                return;
            }
        }
    }

    private static async Task<ConsoleKeyInfo> ReadKeyAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            if (Console.KeyAvailable)
            {
                return Console.ReadKey(intercept: true);
            }

            await Task.Delay(50, cancellationToken).ConfigureAwait(false);
        }

        throw new OperationCanceledException(cancellationToken);
    }

    private static void WriteControls(ILogger logger)
    {
        logger.LogInformation("server-ready");
        logger.LogInformation("keys: Ctrl+R start | Ctrl+S stop | Ctrl+C exit");
    }
}

