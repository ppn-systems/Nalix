// File: TransientConsoleScope.cs
// Purpose: Open a brand-new console window for a temporary report and then restore.

namespace Nalix.Logging.Interop;

/// <summary>
/// Creates a new console window for reporting purposes and restores the previous console state upon disposal.
/// </summary>
[System.Diagnostics.DebuggerNonUserCode]
[System.Runtime.Versioning.SupportedOSPlatform("windows")]
public sealed class TransientConsoleScope : System.IDisposable
{
    private static System.Int32 _refCount = 0;
    private static System.IntPtr _hPrivIn = System.IntPtr.Zero;
    private static System.IntPtr _hPrivOut = System.IntPtr.Zero;

    /// <summary>
    /// Asserts whether a transient console is currently active.
    /// </summary>
    public static System.Boolean IsActive => _refCount > 0;

    /// <summary>
    /// Creates a new console report scope with the specified title, dimensions, and ANSI support.
    /// </summary>
    public TransientConsoleScope(System.String? title = null, System.Int16 cols = 120, System.Int16 rows = 30, System.Boolean enableAnsi = true)
    {
        ConsoleGate.EnterExclusive();

        if (_refCount == 0)
        {
            if (Kernel32.GET_CONSOLE_WINDOW() != System.IntPtr.Zero)
            {
                _ = Kernel32.FREE_CONSOLE();
            }

            if (!Kernel32.ALLOC_CONSOLE())
            {
                throw new System.InvalidOperationException(
                    $"AllocConsole failed: {System.Runtime.InteropServices.Marshal.GetLastPInvokeError()}");
            }

            _hPrivOut = Kernel32.CREATE_FILE_W("CONOUT$",
                        Kernel32.GENERIC_READ | Kernel32.GENERIC_WRITE,
                        Kernel32.FILE_SHARE_READ | Kernel32.FILE_SHARE_WRITE, System.IntPtr.Zero,
                        Kernel32.OPEN_EXISTING, 0, System.IntPtr.Zero);

            if (_hPrivOut == System.IntPtr.Zero || _hPrivOut == (System.IntPtr)(-1))
            {
                throw new System.InvalidOperationException(
                    $"CreateFile(CONOUT$) failed: {System.Runtime.InteropServices.Marshal.GetLastPInvokeError()}");
            }

            _hPrivIn = Kernel32.CREATE_FILE_W("CONIN$",
                       Kernel32.GENERIC_READ | Kernel32.GENERIC_WRITE,
                       Kernel32.FILE_SHARE_READ | Kernel32.FILE_SHARE_WRITE, System.IntPtr.Zero,
                       Kernel32.OPEN_EXISTING, 0, System.IntPtr.Zero);

            if (_hPrivIn == System.IntPtr.Zero || _hPrivIn == (System.IntPtr)(-1))
            {
                throw new System.InvalidOperationException(
                    $"CreateFile(CONIN$) failed: {System.Runtime.InteropServices.Marshal.GetLastPInvokeError()}");
            }

            Kernel32.SET_STD_HANDLE(Kernel32.STD_OUTPUT_HANDLE, _hPrivOut);
            Kernel32.SET_STD_HANDLE(Kernel32.STD_ERROR_HANDLE, _hPrivOut);
            Kernel32.SET_STD_HANDLE(Kernel32.STD_INPUT_HANDLE, _hPrivIn);
            REBIND_SYSTEM_CONSOLE_STREAMS();

            if (!System.String.IsNullOrEmpty(title))
            {
                _ = Kernel32.SET_CONSOLE_TITLE(title);
            }

            if (enableAnsi && Kernel32.GET_CONSOLE_MODE(_hPrivOut, out System.UInt32 mode))
            {
                System.UInt32 desired = mode | Kernel32.ENABLE_VIRTUAL_TERMINAL_PROCESSING | Kernel32.ENABLE_PROCESSED_OUTPUT | Kernel32.ENABLE_WRAP_AT_EOL_OUTPUT;
                _ = Kernel32.SET_CONSOLE_MODE(_hPrivOut, desired); // best-effort
            }

            if (cols > 0 && rows > 0)
            {
                Kernel32.SET_WINDOW_SIZE(cols, rows);
                Kernel32.SET_BUFFER_SIZE(System.Math.Max(cols, (System.Int16)3000), System.Math.Max(rows, (System.Int16)1000));
            }
        }

        _refCount++;
    }

    /// <summary>
    /// Writes a line of text to the console.
    /// </summary>
    public static void WriteLine(System.String message)
    {
        if (_hPrivOut == System.IntPtr.Zero || _hPrivOut == (System.IntPtr)(-1))
        {
            throw new System.InvalidOperationException("Console output handle is invalid.");
        }

        System.Byte[] bytes = System.Text.Encoding.UTF8.GetBytes(message + System.Environment.NewLine);
        if (!Kernel32.WRITE_CONSOLE_W(_hPrivOut, message + System.Environment.NewLine, (System.Int32)bytes.Length + 2, out System.Int32 _, System.IntPtr.Zero))
        {
            throw new System.InvalidOperationException(
                $"WriteFile to console failed: {System.Runtime.InteropServices.Marshal.GetLastPInvokeError()}");
        }
    }

    /// <summary>
    /// Reads a single key press from the console input.
    /// </summary>
    public static void ReadKey()
    {
        const System.String ch = "\0";

        if (_hPrivIn == System.IntPtr.Zero || _hPrivIn == (System.IntPtr)(-1))
        {
            throw new System.InvalidOperationException("Console input handle is invalid.");
        }

        _ = Kernel32.READ_CONSOLE_W(_hPrivIn, ch, 1, out System.UInt32 _, System.IntPtr.Zero);
    }

    /// <summary>
    /// Disposes the console report scope, restoring the previous console state if applicable.
    /// </summary>
    public void Dispose()
    {
        if (_refCount <= 0)
        {
            return;
        }

        _refCount--;

        if (_refCount > 0)
        {
            return;
        }

        if (_hPrivOut != System.IntPtr.Zero && _hPrivOut != (System.IntPtr)(-1))
        {
            _ = Kernel32.CLOSE_HANDLE(_hPrivOut);
            _hPrivOut = System.IntPtr.Zero;
        }
        if (_hPrivIn != System.IntPtr.Zero && _hPrivIn != (System.IntPtr)(-1))
        {
            _ = Kernel32.CLOSE_HANDLE(_hPrivIn);
            _hPrivIn = System.IntPtr.Zero;
        }

        _ = Kernel32.FREE_CONSOLE();

        if (!Kernel32.ATTACH_CONSOLE(Kernel32.ATTACH_PARENT_PROCESS))
        {
            if (!Kernel32.ALLOC_CONSOLE())
            {
                ConsoleGate.ExitExclusive();
                return;
            }
        }

        System.IntPtr hOut = Kernel32.CREATE_FILE_W("CONOUT$",
                             Kernel32.GENERIC_WRITE | Kernel32.GENERIC_READ,
                             Kernel32.FILE_SHARE_WRITE | Kernel32.FILE_SHARE_READ,
                             System.IntPtr.Zero, Kernel32.OPEN_EXISTING, 0, System.IntPtr.Zero);

        System.IntPtr hIn = Kernel32.CREATE_FILE_W("CONIN$",
                            Kernel32.GENERIC_WRITE | Kernel32.GENERIC_READ,
                            Kernel32.FILE_SHARE_WRITE | Kernel32.FILE_SHARE_READ,
                            System.IntPtr.Zero, Kernel32.OPEN_EXISTING, 0, System.IntPtr.Zero);

        if (hOut != System.IntPtr.Zero && hOut != (System.IntPtr)(-1) &&
            hIn != System.IntPtr.Zero && hIn != (System.IntPtr)(-1))
        {
            Kernel32.SET_STD_HANDLE(Kernel32.STD_OUTPUT_HANDLE, hOut);
            Kernel32.SET_STD_HANDLE(Kernel32.STD_ERROR_HANDLE, hOut);
            Kernel32.SET_STD_HANDLE(Kernel32.STD_INPUT_HANDLE, hIn);
            REBIND_SYSTEM_CONSOLE_STREAMS();
        }

        ConsoleGate.ExitExclusive();
    }

    private static void REBIND_SYSTEM_CONSOLE_STREAMS()
    {
        System.IO.Stream stdIn = System.Console.OpenStandardInput();
        System.IO.Stream stdErr = System.Console.OpenStandardError();
        System.IO.Stream stdOut = System.Console.OpenStandardOutput();

        System.IO.StreamReader inReader = new(stdIn);
        System.IO.StreamWriter outWriter = new(stdOut) { AutoFlush = true };
        System.IO.StreamWriter errWriter = new(stdErr) { AutoFlush = true };

        System.Console.SetIn(inReader);
        System.Console.SetOut(outWriter);
        System.Console.SetError(errWriter);
    }
}
