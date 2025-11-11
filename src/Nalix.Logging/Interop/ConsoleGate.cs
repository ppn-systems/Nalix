// Copyright (c) 2025 PPN Corporation. All rights reserved.

namespace Nalix.Logging.Interop;

/// <summary>
/// Synchronization gate for console access. A transient scope acquires an exclusive (write) lock,
/// while log targets take shared (read) locks. When the scope is active, log writers must wait.
/// </summary>
internal static class ConsoleGate
{
    private static readonly System.Threading.ReaderWriterLockSlim _rw =
        new(System.Threading.LockRecursionPolicy.SupportsRecursion);

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
}
