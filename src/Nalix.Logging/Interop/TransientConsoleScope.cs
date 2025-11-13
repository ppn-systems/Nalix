// Copyright (c) 2025 PPN Corporation. All rights reserved.

namespace Nalix.Logging.Interop;

/// <summary>
/// Creates a new console window for reporting purposes and restores the previous console state upon disposal.
/// </summary>
[System.Diagnostics.DebuggerNonUserCode]
public sealed class TransientConsoleScope : System.IDisposable
{
    private static System.Int32 _refCount = 0;
    private static System.IntPtr _hPrivIn = System.IntPtr.Zero;
    private static System.IntPtr _hPrivOut = System.IntPtr.Zero;

    private static readonly System.Threading.ReaderWriterLockSlim _rw = new(System.Threading.LockRecursionPolicy.SupportsRecursion);
    private static readonly System.Threading.Mutex _globalConsoleMux = new(initiallyOwned: false, name: @"Global\Nalix.TransientConsole");

    private static System.Boolean _ownsGlobalMux = false;

    /// <summary>
    /// Creates a new console report scope with the specified title, dimensions, and ANSI support.
    /// </summary>

    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    public TransientConsoleScope(System.String? title = null, System.Int16 cols = 120, System.Int16 rows = 30, System.Boolean enableAnsi = true)
    {
        EnterExclusive();

        // Acquire global (cross-process) mutex only for the *first* scope in this process.
        // This ensures only one process at a time is allowed to ALLOC_CONSOLE for a "transient" window.
        if (_refCount == 0)
        {
            // Try to acquire immediately; you can change timeout if muốn "đợi".
            _ownsGlobalMux = _globalConsoleMux.WaitOne(0);
            if (!_ownsGlobalMux)
            {
                // Another process is already holding the console-creation right.
                // Choose behavior: throw OR best-effort attach to parent (no transient).
                // Option A (strict): throw
                // throw new System.InvalidOperationException("Another process already owns the transient console.");

                // Option B (benign fallback): attach to parent console and keep going without ALLOC_CONSOLE.
                // This still respects the rule: we don't create an extra console.
                if (!Kernel32.ATTACH_CONSOLE(Kernel32.ATTACH_PARENT_PROCESS))
                {
                    // As a last resort, you can still bail out:
                    throw new System.InvalidOperationException("Transient console is already in use by another process.");
                }
            }
        }

        if (_refCount == 0 && _ownsGlobalMux)
        {
            if (Kernel32.GET_CONSOLE_WINDOW() != System.IntPtr.Zero)
            {
                _ = Kernel32.FREE_CONSOLE();
            }

            if (!Kernel32.ALLOC_CONSOLE())
            {
                _ownsGlobalMux = false;
                try
                {
                    _globalConsoleMux.ReleaseMutex();
                }
                catch { /* ignored */ }

                throw new System.InvalidOperationException(
                    $"AllocConsole failed: {System.Runtime.InteropServices.Marshal.GetLastPInvokeError()}");
            }

            _hPrivOut = Kernel32.CREATE_FILE_W("CONOUT$",
                        Kernel32.GENERIC_READ | Kernel32.GENERIC_WRITE,
                        Kernel32.FILE_SHARE_READ | Kernel32.FILE_SHARE_WRITE, System.IntPtr.Zero,
                        Kernel32.OPEN_EXISTING, 0, System.IntPtr.Zero);

            if (_hPrivOut == System.IntPtr.Zero || _hPrivOut == (System.IntPtr)(-1))
            {
                _ownsGlobalMux = false;
                try
                {
                    _globalConsoleMux.ReleaseMutex();
                }
                catch { /* ignored */ }

                throw new System.InvalidOperationException(
                    $"CreateFile(CONOUT$) failed: {System.Runtime.InteropServices.Marshal.GetLastPInvokeError()}");
            }

            _hPrivIn = Kernel32.CREATE_FILE_W("CONIN$",
                       Kernel32.GENERIC_READ | Kernel32.GENERIC_WRITE,
                       Kernel32.FILE_SHARE_READ | Kernel32.FILE_SHARE_WRITE, System.IntPtr.Zero,
                       Kernel32.OPEN_EXISTING, 0, System.IntPtr.Zero);

            if (_hPrivIn == System.IntPtr.Zero || _hPrivIn == (System.IntPtr)(-1))
            {
                _ownsGlobalMux = false;
                try
                {
                    _globalConsoleMux.ReleaseMutex();
                }
                catch { /* ignored */ }

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

    /// <summary>Enter an exclusive section (blocks all shared sections).</summary>
    public static void EnterExclusive() => _rw.EnterWriteLock();

    /// <summary>Exit an exclusive section.</summary>
    public static void ExitExclusive()
    {
        if (_rw.IsWriteLockHeld)
        {
            _rw.ExitWriteLock();
        }
    }

    /// <summary>Enter a shared (read) section that will wait if exclusive is held.</summary>
    public static void EnterShared() => _rw.EnterReadLock();

    /// <summary>Exit a shared section.</summary>
    public static void ExitShared()
    {
        if (_rw.IsReadLockHeld)
        {
            _rw.ExitReadLock();
        }
    }

    /// <summary>
    /// Helper disposable for shared sections: <c>using (ConsoleGate.Shared()) { ... }</c>
    /// </summary>
    public static System.IDisposable Shared() => new SharedCookie();

    private readonly struct SharedCookie : System.IDisposable
    {
        public SharedCookie()
        {
            _rw.EnterReadLock();
        }
        public void Dispose()
        {
            if (_rw.IsReadLockHeld)
            {
                _rw.ExitReadLock();
            }
        }
    }

    /// <summary>
    /// Writes a line of text to the console.
    /// </summary>

    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
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

    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
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

    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
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
                if (_ownsGlobalMux)
                {
                    _ownsGlobalMux = false;
                    try
                    {
                        _globalConsoleMux.ReleaseMutex();
                    }
                    catch { /* ignored */ }
                }

                ExitExclusive();
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

        if (_ownsGlobalMux)
        {
            _ownsGlobalMux = false;
            try
            {
                _globalConsoleMux.ReleaseMutex();
            }
            catch { /* ignored */ }
        }

        ExitExclusive();
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
