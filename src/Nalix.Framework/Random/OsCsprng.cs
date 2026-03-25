// Copyright (c) 2025 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

#if DEBUG
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Nalix.Framework.Tests.")]
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Nalix.Framework.Benchmarks")]
#endif

namespace Nalix.Framework.Random.Core;

/// <summary>
/// Provides cryptographically secure random number generation using the operating system's CSPRNG facilities.
/// </summary>
[System.Diagnostics.StackTraceHidden]
[System.Diagnostics.DebuggerStepThrough]
[System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
internal static partial class OsCsprng
{
    #region Fields

    // Cached platform dispatcher (obfuscated)
    private static System.Action<System.Span<byte>> _f;

    // Lazy /dev/urandom handle for Unix fallback
    private static System.IO.FileStream? s_devUrandom;
    private static readonly System.Threading.Lock s_devUrandomLock = new();

    // Linux error codes we care about
    private const int EINTR = 4;
    private const int ENOSYS = 38;

    // Windows CNG flag
    private const uint C = 0x00000002;

    #endregion Fields

    #region Constructor

    static OsCsprng()
    {
        try
        {
            _f = System.OperatingSystem.IsWindows()
                ? W
                : System.OperatingSystem.IsLinux()
                    ? L
                    : System.OperatingSystem.IsMacOS() ||
                     System.OperatingSystem.IsIOS() ||
                     System.OperatingSystem.IsTvOS() ||
                     System.OperatingSystem.IsWatchOS()
                    ? A
                    : D;
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
    public static void Fill([System.Diagnostics.CodeAnalysis.NotNull] System.Span<byte> buffer)
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
    private static partial int BCryptGenRandom(
        nint hAlgorithm,
        System.Span<byte> pbBuffer,
        int cbBuffer, uint dwFlags);

    /// <summary>
    /// Windows-specific CSPRNG implementation using BCryptGenRandom (CNG).
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    private static void W(System.Span<byte> b)
    {
        int s = BCryptGenRandom(nint.Zero, b, b.Length, C);
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
    private static partial nint getrandom(nint buf, nint buflen, uint flags);

    /// <summary>
    /// Linux-specific CSPRNG implementation using getrandom() syscall.
    /// Falls back to /dev/urandom if getrandom is not supported (ENOSYS).
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    [System.Runtime.Versioning.SupportedOSPlatform("linux")]
    private static unsafe void L(System.Span<byte> b)
    {
        fixed (byte* p = b)
        {
            nuint t = nuint.Zero;
            nuint n = (nuint)b.Length;

            while (t < n)
            {
                nint r0 = getrandom((nint)(p + (nint)t), (nint)(n - t), 0);
                long r = r0.ToInt64();
                if (r < 0)
                {
                    int errno = System.Runtime.InteropServices.Marshal.GetLastPInvokeError();

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
                        int offset = (int)t;
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

                t += (nuint)r;
            }
        }
    }

    // -------------------- Apple: SecRandomCopyBytes --------------------

    /// <summary>
    /// P/Invoke declaration for Apple SecRandomCopyBytes function.
    /// </summary>
    [System.Runtime.InteropServices.LibraryImport(
        "/SYSTEM/Library/Frameworks/Security.framework/Security", EntryPoint = "SecRandomCopyBytes")]
    private static partial int SecRandomCopyBytes(nint rnd, nint count, nint bytes);

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
    private static unsafe void A(System.Span<byte> b)
    {
        fixed (byte* p = b)
        {
            int s = SecRandomCopyBytes(nint.Zero, b.Length, (nint)p);
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
    private static void D(System.Span<byte> b)
    {
        System.IO.FileStream fs = GetDevUrandom();

        int total = 0;
        // FileStream is not thread-safe -> synchronize reads
        lock (s_devUrandomLock)
        {
            while (total < b.Length)
            {
                int r = fs.Read(b[total..]);
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
