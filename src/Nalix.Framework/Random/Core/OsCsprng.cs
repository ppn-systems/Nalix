// Copyright (c) 2025 PPN Corporation. All rights reserved.

namespace Nalix.Framework.Random.Core;

/// <summary>
/// Provides cryptographically secure random number generation using the operating system's CSPRNG facilities.
/// </summary>
[System.Diagnostics.StackTraceHidden]
[System.Diagnostics.DebuggerStepThrough]
internal static partial class OsCsprng
{
    #region Fields

    // Cached platform dispatcher (obfuscated)
    private static System.Action<System.Span<System.Byte>> _f;

    // Lazy /dev/urandom handle for Unix fallback
    private static System.IO.FileStream? s_devUrandom;
    private static readonly System.Threading.Lock s_devUrandomLock = new();

    // Linux error codes we care about
    private const System.Int32 EINTR = 4;
    private const System.Int32 ENOSYS = 38;

    // Windows CNG flag
    private const System.UInt32 C = 0x00000002;

    #endregion Fields

    #region Constructor

    static OsCsprng()
    {
        try
        {
            if (System.OperatingSystem.IsWindows())
            {
                _f = W;
            }
            else
            {
                _f = System.OperatingSystem.IsLinux()
                    ? L
                    : System.OperatingSystem.IsMacOS() ||
                     System.OperatingSystem.IsIOS() ||
                     System.OperatingSystem.IsTvOS() ||
                     System.OperatingSystem.IsWatchOS()
                    ? A
                    : D;
            }
        }
        catch
        {
            _f = OsRandom.Fill;
        }
    }

    #endregion Constructor

    #region APIs

    /// <summary>
    /// Fills the specified buffer with cryptographically secure random bytes using the operating system's CSPRNG facilities.
    /// </summary>
    /// <param name="buffer">The buffer to fill with random bytes.</param>
    /// <remarks>
    /// Thread-safe. Uses platform-specific implementations:
    /// - Windows: BCryptGenRandom (CNG)
    /// - Linux: getrandom() syscall with /dev/urandom fallback
    /// - macOS/iOS/tvOS/watchOS: SecRandomCopyBytes
    /// - Other platforms: /dev/urandom
    /// Falls back to OsRandom (non-cryptographic) if OS CSPRNG is unavailable.
    /// </remarks>
    /// <exception cref="System.InvalidOperationException">Thrown when OS CSPRNG is unavailable and fallback fails.</exception>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static void Fill([System.Diagnostics.CodeAnalysis.NotNull] System.Span<System.Byte> buffer)
    {
        if (buffer.Length == 0)
        {
            return;
        }

        _f(buffer);
    }

    #endregion APIs

    #region Private

    // -------------------- Windows (CNG) --------------------

    /// <summary>
    /// P/Invoke declaration for Windows BCryptGenRandom function.
    /// </summary>
    [System.Runtime.InteropServices.LibraryImport("Bcrypt.dll")]
    private static partial System.Int32 BCryptGenRandom(
        System.IntPtr hAlgorithm,
        System.Span<System.Byte> pbBuffer,
        System.Int32 cbBuffer, System.UInt32 dwFlags);

    /// <summary>
    /// Windows-specific CSPRNG implementation using BCryptGenRandom (CNG).
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    private static void W(System.Span<System.Byte> b)
    {
        System.Int32 s = BCryptGenRandom(System.IntPtr.Zero, b, b.Length, C);
        if (s != 0)
        {
            throw new System.InvalidOperationException($"OS CSPRNG unavailable (BCryptGenRandom failed with code 0x{s:X}).");
        }
    }

    // -------------------- Linux: getrandom --------------------

    /// <summary>
    /// P/Invoke declaration for Linux getrandom syscall.
    /// </summary>
    [System.Runtime.InteropServices.LibraryImport("libc", SetLastError = true)]
    private static partial System.IntPtr getrandom(System.IntPtr buf, System.IntPtr buflen, System.UInt32 flags);

    /// <summary>
    /// Linux-specific CSPRNG implementation using getrandom() syscall.
    /// Falls back to /dev/urandom if getrandom is not supported (ENOSYS).
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    [System.Runtime.Versioning.SupportedOSPlatform("linux")]
    private static unsafe void L(System.Span<System.Byte> b)
    {
        fixed (System.Byte* p = b)
        {
            System.UIntPtr t = System.UIntPtr.Zero;
            System.UIntPtr n = (System.UIntPtr)b.Length;

            while (t < n)
            {
                System.IntPtr r0 = getrandom((System.IntPtr)(p + (System.IntPtr)t), (System.IntPtr)(n - t), 0);
                System.Int64 r = r0.ToInt64();
                if (r < 0)
                {
                    System.Int32 errno = System.Runtime.InteropServices.Marshal.GetLastPInvokeError();

                    // EINTR: retry
                    if (errno == EINTR)
                    {
                        continue;
                    }

                    // ENOSYS: kernel does not support getrandom -> permanently switch to /dev/urandom fallback
                    if (errno == ENOSYS)
                    {
                        // switch dispatcher once so future calls skip getrandom
                        _f = D;

                        // fill the remaining part via fallback
                        System.Int32 offset = (System.Int32)t;
                        if (offset < b.Length)
                        {
                            D(b[offset..]);
                        }
                        return;
                    }

                    throw new System.InvalidOperationException($"OS CSPRNG unavailable (getrandom failed with errno={errno}).");
                }

                if (r == 0)
                {
                    // Defensive: zero progress from getrandom should not loop forever
                    throw new System.InvalidOperationException("OS CSPRNG unavailable (getrandom returned 0 bytes).");
                }

                t += (System.UIntPtr)r;
            }
        }
    }

    // -------------------- Apple: SecRandomCopyBytes --------------------

    /// <summary>
    /// P/Invoke declaration for Apple SecRandomCopyBytes function.
    /// </summary>
    [System.Runtime.InteropServices.LibraryImport(
        "/SYSTEM/Library/Frameworks/Security.framework/Security", EntryPoint = "SecRandomCopyBytes")]
    private static partial System.Int32 SecRandomCopyBytes(System.IntPtr rnd, System.IntPtr count, System.IntPtr bytes);

    /// <summary>
    /// Apple platform CSPRNG implementation using SecRandomCopyBytes.
    /// Falls back to /dev/urandom if SecRandomCopyBytes fails.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    [System.Runtime.Versioning.SupportedOSPlatform("ios")]
    [System.Runtime.Versioning.SupportedOSPlatform("tvos")]
    [System.Runtime.Versioning.SupportedOSPlatform("macos")]
    [System.Runtime.Versioning.SupportedOSPlatform("watchos")]
    private static unsafe void A(System.Span<System.Byte> b)
    {
        fixed (System.Byte* p = b)
        {
            System.Int32 s = SecRandomCopyBytes(System.IntPtr.Zero, b.Length, (System.IntPtr)p);
            if (s != 0)
            {
                _f = D;
                D(b);
            }
        }
    }

    // -------------------- Fallback: /dev/urandom --------------------

    /// <summary>
    /// Fallback CSPRNG implementation using /dev/urandom.
    /// Used on platforms without native CSPRNG support or when native APIs fail.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static void D(System.Span<System.Byte> b)
    {
        System.IO.FileStream fs = GetDevUrandom();

        System.Int32 total = 0;
        // FileStream is not thread-safe -> synchronize reads
        lock (s_devUrandomLock)
        {
            while (total < b.Length)
            {
                System.Int32 r = fs.Read(b[total..]);
                if (r <= 0)
                {
                    throw new System.InvalidOperationException("OS CSPRNG unavailable (/dev/urandom returned 0 bytes).");
                }

                total += r;
            }
        }
    }

    /// <summary>
    /// Gets a cached FileStream for /dev/urandom (created lazily).
    /// Thread-safe double-checked locking pattern.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static System.IO.FileStream GetDevUrandom()
    {
        System.IO.FileStream? fs = System.Threading.Volatile.Read(ref s_devUrandom);
        if (fs is not null)
        {
            return fs;
        }

        lock (s_devUrandomLock)
        {
            fs = s_devUrandom;
            if (fs is not null)
            {
                return fs;
            }

            fs = new System.IO.FileStream(
                "/dev/urandom",
                System.IO.FileMode.Open,
                System.IO.FileAccess.Read,
                System.IO.FileShare.Read,
                bufferSize: 0,
                options: System.IO.FileOptions.SequentialScan);

            System.Threading.Volatile.Write(ref s_devUrandom, fs);
            return fs;
        }
    }

    #endregion Private
}
