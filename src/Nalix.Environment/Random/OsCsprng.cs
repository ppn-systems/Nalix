// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using Nalix.Abstractions.Exceptions;

#if DEBUG
[assembly: InternalsVisibleTo("Nalix.Framework.Tests")]
[assembly: InternalsVisibleTo("Nalix.Framework.Benchmarks")]
#endif

namespace Nalix.Environment.Random;

/// <summary>
/// Provides cryptographically secure random number generation using the operating system's CSPRNG facilities.
/// </summary>
[StackTraceHidden]
[DebuggerStepThrough]
[EditorBrowsable(EditorBrowsableState.Never)]
internal static partial class OsCsprng
{
    #region Fields

    // Cached platform dispatcher (obfuscated)
    private static Action<Span<byte>> s_f;

    // Lazy /dev/urandom handle for Unix fallback
    private static FileStream? s_devUrandom;
    private static readonly Lock s_devUrandomLock = new();

    // Linux error codes we care about
    private const int EINTR = 4;
    private const int ENOSYS = 38;

    // Windows CNG flag
    private const uint C = 0x00000002;

    #endregion Fields

    #region Constructor

    static OsCsprng()
    {
        s_f = OperatingSystem.IsWindows()
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
    /// <exception cref="InvalidOperationException">Thrown when OS CSPRNG is unavailable and fallback fails.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Fill(Span<byte> buffer)
    {
        if (buffer.Length == 0)
        {
            return;
        }

        s_f(buffer);
    }

    #endregion APIs

    #region Private

    // -------------------- Windows (CNG) --------------------

    /// <summary>
    /// P/Invoke declaration for Windows BCryptGenRandom function.
    /// </summary>
    [LibraryImport("Bcrypt.dll")]
    private static partial int BCryptGenRandom(nint hAlgorithm, Span<byte> pbBuffer, int cbBuffer, uint dwFlags);

    /// <summary>
    /// Windows-specific CSPRNG implementation using BCryptGenRandom (CNG).
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    private static void W(Span<byte> b)
    {
        int status = BCryptGenRandom(nint.Zero, b, b.Length, C);
        if (status != 0)
        {
            throw new CipherException($"BCryptGenRandom failed: status=0x{status:X}.");
        }
    }

    // -------------------- Linux: getrandom --------------------

    /// <summary>
    /// P/Invoke declaration for Linux getrandom syscall.
    /// </summary>
    [LibraryImport("libc", SetLastError = true)]
    private static partial nint getrandom(nint buf, nint buflen, uint flags);

    /// <summary>
    /// Linux-specific CSPRNG implementation using getrandom() syscall.
    /// Falls back to /dev/urandom if getrandom is not supported (ENOSYS).
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    [System.Runtime.Versioning.SupportedOSPlatform("linux")]
    private static unsafe void L(Span<byte> b)
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
                    int errno = Marshal.GetLastPInvokeError();

                    // EINTR: retry
                    if (errno == EINTR)
                    {
                        continue;
                    }

                    // ENOSYS: kernel does not support getrandom -> permanently switch to /dev/urandom fallback
                    if (errno == ENOSYS)
                    {
                        // switch dispatcher once so future calls skip getrandom
                        Volatile.Write(ref s_f, D);

                        // fill the remaining part via fallback
                        int offset = (int)t;
                        if (offset < b.Length)
                        {
                            D(b[offset..]);
                        }
                        return;
                    }

                    throw new CipherException($"OS CSPRNG unavailable (getrandom failed with errno={errno}).");
                }

                if (r == 0)
                {
                    // Defensive: zero progress from getrandom should not loop forever
                    throw new CipherException("OS CSPRNG unavailable (getrandom returned 0 bytes).");
                }

                t += (nuint)r;
            }
        }
    }

    // -------------------- Apple: SecRandomCopyBytes --------------------

    /// <summary>
    /// P/Invoke declaration for Apple SecRandomCopyBytes function.
    /// </summary>
    [LibraryImport(
        "/SYSTEM/Library/Frameworks/Security.framework/Security", EntryPoint = "SecRandomCopyBytes")]
    private static partial int SecRandomCopyBytes(nint rnd, nint count, nint bytes);

    /// <summary>
    /// Apple platform CSPRNG implementation using SecRandomCopyBytes.
    /// Falls back to /dev/urandom if SecRandomCopyBytes fails.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    [System.Runtime.Versioning.SupportedOSPlatform("ios")]
    [System.Runtime.Versioning.SupportedOSPlatform("tvos")]
    [System.Runtime.Versioning.SupportedOSPlatform("macos")]
    [System.Runtime.Versioning.SupportedOSPlatform("watchos")]
    private static unsafe void A(Span<byte> b)
    {
        fixed (byte* p = b)
        {
            int s = SecRandomCopyBytes(nint.Zero, b.Length, (nint)p);
            if (s != 0)
            {
                Volatile.Write(ref s_f, D);
                D(b);
            }
        }
    }

    // -------------------- Fallback: /dev/urandom --------------------

    /// <summary>
    /// Fallback CSPRNG implementation using /dev/urandom.
    /// Used on platforms without native CSPRNG support or when native APIs fail.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void D(Span<byte> b)
    {
#pragma warning disable CA2000 // GetDevUrandom transfers ownership to the static cache; the stream is intentionally process-lifetime cached.
        FileStream fs = GetDevUrandom();
#pragma warning restore CA2000

        int total = 0;
        // FileStream is not thread-safe -> synchronize reads
        lock (s_devUrandomLock)
        {
            while (total < b.Length)
            {
                int r = fs.Read(b[total..]);
                if (r <= 0)
                {
                    throw new CipherException("OS CSPRNG unavailable (/dev/urandom returned 0 bytes).");
                }

                total += r;
            }
        }
    }

    /// <summary>
    /// Gets a cached FileStream for /dev/urandom (created lazily).
    /// Thread-safe double-checked locking pattern.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static FileStream GetDevUrandom()
    {
        FileStream? fs = Volatile.Read(ref s_devUrandom);
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

            fs = new FileStream(
                "/dev/urandom",
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 0,
                options: FileOptions.SequentialScan);

            Volatile.Write(ref s_devUrandom, fs);
            return fs;
        }
    }

    #endregion Private
}
