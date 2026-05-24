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

        TrySetCursorVisible(false);
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            exit.Cancel();
        };

        await using NetworkApplication host = Startup.Configure(logger);

        try
        {
            // Auto-activate the server on startup
            await host.ActivateAsync(exit.Token).ConfigureAwait(false);
            s_listeningMessage(logger, Startup.ListenAddress, Startup.ListenPort, null);

            Console.WriteLine($"[DEBUG] Server BenchmarkPacket Magic: 0x{Nalix.Codec.DataFrames.PacketSchema<Nalix.Codec.ProtocolFrames.BenchmarkPacket>.AutoMagic:X8}");

            WriteControls(logger);

            try
            {
                if (!Console.IsInputRedirected)
                {
                    await RunCommandLoopAsync(host, logger, exit.Token).ConfigureAwait(false);
                }
                else
                {
                    // Background execution: wait indefinitely until canceled
                    await Task.Delay(Timeout.InfiniteTimeSpan, exit.Token).ConfigureAwait(false);
                }
            }
            catch (Exception loopEx) when (loopEx is InvalidOperationException or IOException)
            {
                logger.LogWarning(loopEx, "Console key listener failed to start. Falling back to background wait mode.");
                await Task.Delay(Timeout.InfiniteTimeSpan, exit.Token).ConfigureAwait(false);
            }

            return 0;
        }
        catch (OperationCanceledException)
        {
            return 0;
        }
        catch (Exception ex)
        {
#pragma warning disable CA1849 // Synchronous write is acceptable for fatal crash reporting
            Console.Error.WriteLine($"[FATAL] Server crashed: {ex}");
#pragma warning restore CA1849
            ILogger activeLogger = InstanceManager.Instance.GetExistingInstance<ILogger>() ?? logger;
            activeLogger.LogError(ex, "server-fatal");

            return -1;
        }
        finally
        {
            await host.DeactivateAsync().ConfigureAwait(false);
            TrySetCursorVisible(true);
            if (logger is IDisposable disposableLogger)
            {
                disposableLogger.Dispose();
            }
        }
    }

    [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Console cursor visibility is best-effort and should not crash headless servers.")]
    private static void TrySetCursorVisible(bool visible)
    {
        try
        {
            Console.CursorVisible = visible;
        }
        catch
        {
            // Ignore console control errors when stdout/stderr is redirected
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

