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

    private static readonly System.IntPtr INVALID_HANDLE_VALUE = new(-1);

    /// <summary>
    /// Creates a new console report scope with the specified title, dimensions, and ANSI support.
    /// </summary>
    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    public TransientConsoleScope(System.String? title = null, System.Int16 cols = 120, System.Int16 rows = 30, System.Boolean enableAnsi = true)
    {
        bool ctorSucceeded = false;
        _rw.EnterWriteLock(); // hold exclusive for lifetime (will be released in Dispose) if ctor succeeds

        try
        {
            // Acquire global (cross-process) mutex only for the *first* scope in this process.
            // This ensures only one process at a time is allowed to ALLOC_CONSOLE for a "transient" window.
            if (_refCount == 0)
            {
                try
                {
                    // Try to acquire immediately; you can change timeout if muốn "đợi".
                    _ownsGlobalMux = _globalConsoleMux.WaitOne(0);
                }
                catch (System.Exception)
                {
                    // If we cannot create or wait on the global mutex, fall back to not owning it.
                    _ownsGlobalMux = false;
                }

                if (!_ownsGlobalMux)
                {
                    // Another process is already holding the console-creation right.
                    // Benign fallback: attach to parent console and keep going without ALLOC_CONSOLE.
                    if (!Kernel32.ATTACH_CONSOLE(Kernel32.ATTACH_PARENT_PROCESS))
                    {
                        // As a last resort, bail out with exception.
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
                    try { _globalConsoleMux.ReleaseMutex(); } catch { /* ignored */ }

                    throw new System.InvalidOperationException(
                        $"AllocConsole failed: {System.Runtime.InteropServices.Marshal.GetLastPInvokeError()}");
                }

                _hPrivOut = Kernel32.CREATE_FILE_W("CONOUT$",
                            Kernel32.GENERIC_READ | Kernel32.GENERIC_WRITE,
                            Kernel32.FILE_SHARE_READ | Kernel32.FILE_SHARE_WRITE, System.IntPtr.Zero,
                            Kernel32.OPEN_EXISTING, 0, System.IntPtr.Zero);

                if (_hPrivOut == System.IntPtr.Zero || _hPrivOut == INVALID_HANDLE_VALUE)
                {
                    _ownsGlobalMux = false;
                    try { _globalConsoleMux.ReleaseMutex(); } catch { /* ignored */ }

                    throw new System.InvalidOperationException(
                        $"CreateFile(CONOUT$) failed: {System.Runtime.InteropServices.Marshal.GetLastPInvokeError()}");
                }

                _hPrivIn = Kernel32.CREATE_FILE_W("CONIN$",
                           Kernel32.GENERIC_READ | Kernel32.GENERIC_WRITE,
                           Kernel32.FILE_SHARE_READ | Kernel32.FILE_SHARE_WRITE, System.IntPtr.Zero,
                           Kernel32.OPEN_EXISTING, 0, System.IntPtr.Zero);

                if (_hPrivIn == System.IntPtr.Zero || _hPrivIn == INVALID_HANDLE_VALUE)
                {
                    _ownsGlobalMux = false;
                    try { _globalConsoleMux.ReleaseMutex(); } catch { /* ignored */ }

                    throw new System.InvalidOperationException(
                        $"CreateFile(CONIN$) failed: {System.Runtime.InteropServices.Marshal.GetLastPInvokeError()}");
                }

                _ = Kernel32.SET_STD_HANDLE(Kernel32.STD_OUTPUT_HANDLE, _hPrivOut);
                _ = Kernel32.SET_STD_HANDLE(Kernel32.STD_ERROR_HANDLE, _hPrivOut);
                _ = Kernel32.SET_STD_HANDLE(Kernel32.STD_INPUT_HANDLE, _hPrivIn);
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
                    _ = Kernel32.SET_WINDOW_SIZE(cols, rows);
                    _ = Kernel32.SET_BUFFER_SIZE(System.Math.Max(cols, (System.Int16)3000), System.Math.Max(rows, (System.Int16)1000));
                }
            }

            _refCount++;
            ctorSucceeded = true; // keep the write lock for the lifetime (must call Dispose to release)
        }
        finally
        {
            if (!ctorSucceeded)
            {
                // If ctor failed we must release the write-lock to avoid leak
                if (_rw.IsWriteLockHeld)
                {
                    _rw.ExitWriteLock();
                }
            }
        }
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
        // Acquire a shared lock for console I/O (best-effort; if caller already holds exclusive, this will nest).
        EnterShared();
        try
        {
            if (_hPrivOut == System.IntPtr.Zero || _hPrivOut == INVALID_HANDLE_VALUE)
            {
                throw new System.InvalidOperationException("Console output handle is invalid.");
            }

            string toWrite = message + System.Environment.NewLine;
            // Use WriteConsoleW semantics: pass character count (not bytes)
            if (!Kernel32.WRITE_CONSOLE_W(_hPrivOut, toWrite, toWrite.Length, out System.Int32 _, System.IntPtr.Zero))
            {
                throw new System.InvalidOperationException(
                    $"WriteConsoleW to console failed: {System.Runtime.InteropServices.Marshal.GetLastPInvokeError()}");
            }
        }
        finally
        {
            ExitShared();
        }
    }

    /// <summary>
    /// Reads a single key press from the console input.
    /// </summary>
    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    public static void ReadKey()
    {
        const System.String ch = "\0";

        EnterShared();
        try
        {
            if (_hPrivIn == System.IntPtr.Zero || _hPrivIn == INVALID_HANDLE_VALUE)
            {
                throw new System.InvalidOperationException("Console input handle is invalid.");
            }

            _ = Kernel32.READ_CONSOLE_W(_hPrivIn, ch, 1, out System.UInt32 _, System.IntPtr.Zero);
            // caller can ignore returned character; this method blocks until a key is read
        }
        finally
        {
            ExitShared();
        }
    }

    /// <summary>
    /// Disposes the console report scope, restoring the previous console state if applicable.
    /// </summary>
    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    public void Dispose()
    {
        // This Dispose assumes that the instance holds the write-lock that was taken in the ctor.
        // If ctor failed, the lock was already released and Dispose should not be called.
        if (_refCount <= 0)
        {
            // nothing to do; ensure we don't release lock we don't hold
            return;
        }

        _refCount--;

        if (_refCount > 0)
        {
            // other scopes still active; keep exclusive lock
            return;
        }

        if (_hPrivOut != System.IntPtr.Zero && _hPrivOut != INVALID_HANDLE_VALUE)
        {
            _ = Kernel32.CLOSE_HANDLE(_hPrivOut);
            _hPrivOut = System.IntPtr.Zero;
        }
        if (_hPrivIn != System.IntPtr.Zero && _hPrivIn != INVALID_HANDLE_VALUE)
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
                    try { _globalConsoleMux.ReleaseMutex(); } catch { /* ignored */ }
                }

                // Finally release exclusive lock and return
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

        if (hOut != System.IntPtr.Zero && hOut != INVALID_HANDLE_VALUE &&
            hIn != System.IntPtr.Zero && hIn != INVALID_HANDLE_VALUE)
        {
            _ = Kernel32.SET_STD_HANDLE(Kernel32.STD_OUTPUT_HANDLE, hOut);
            _ = Kernel32.SET_STD_HANDLE(Kernel32.STD_ERROR_HANDLE, hOut);
            _ = Kernel32.SET_STD_HANDLE(Kernel32.STD_INPUT_HANDLE, hIn);
            REBIND_SYSTEM_CONSOLE_STREAMS();
        }

        if (_ownsGlobalMux)
        {
            _ownsGlobalMux = false;
            try { _globalConsoleMux.ReleaseMutex(); } catch { /* ignored */ }
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