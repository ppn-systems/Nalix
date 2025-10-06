// Copyright (c) 2025 PPN Corporation. All rights reserved.

namespace Nalix.Framework.Identity;


/// <summary>
/// Provides cryptographically secure random number generation using the operating system's CSPRNG facilities.
/// </summary>
[System.Diagnostics.StackTraceHidden]
[System.Diagnostics.DebuggerStepThrough]
[System.Runtime.CompilerServices.SkipLocalsInit]
public static partial class OsRandom
{
    #region Fields

    // Cached platform dispatcher (obfuscated)
    private static readonly System.Action<System.Span<System.Byte>> _f;

    #endregion Fields

    #region Constructor

    static OsRandom()
    {
        if (System.OperatingSystem.IsWindows())
        {
            _f = W;
        }
        else if (System.OperatingSystem.IsLinux())
        {
            _f = L;
        }
        else if (System.OperatingSystem.IsMacOS() ||
                 System.OperatingSystem.IsIOS() ||
                 System.OperatingSystem.IsTvOS() ||
                 System.OperatingSystem.IsWatchOS())
        {
            _f = A;
        }
        else
        {
            _f = D;
        }
    }

    #endregion Constructor

    #region APIs

    /// <summary>
    /// Fills the specified buffer with cryptographically secure random bytes using the operating system's CSPRNG facilities.
    /// </summary>
    /// <param name="buffer">The buffer to fill with random bytes.</param>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static void Fill(System.Span<System.Byte> buffer)
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

    private const System.UInt32 C = 0x00000002;

    [System.Runtime.InteropServices.LibraryImport("Bcrypt.dll")]
    private static partial int BCryptGenRandom(
        System.IntPtr hAlgorithm,
        System.Span<System.Byte> pbBuffer,
        System.Int32 cbBuffer, System.UInt32 dwFlags);

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static void W(System.Span<System.Byte> b)
    {
        System.Int32 s = BCryptGenRandom(System.IntPtr.Zero, b, b.Length, C);
        if (s != 0)
        {
            throw new System.InvalidOperationException("OS CSPRNG unavailable.");
        }
    }

    // -------------------- Linux: getrandom --------------------

    [System.Runtime.InteropServices.LibraryImport("libc", SetLastError = true)]
    private static partial System.IntPtr getrandom(System.IntPtr buf, System.IntPtr buflen, System.UInt32 flags);

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static unsafe void L(System.Span<System.Byte> b)
    {
        fixed (System.Byte* p = b)
        {
            System.UIntPtr t = 0, n = (System.UIntPtr)b.Length;
            while (t < n)
            {
                System.IntPtr r0 = getrandom((System.IntPtr)(p + t), (System.IntPtr)(n - t), 0);
                System.Int64 r = r0.ToInt64();
                if (r < 0)
                {
                    System.Int32 errno = System.Runtime.InteropServices.Marshal.GetLastPInvokeError();
                    // ENOSYS (38)
                    // EINTR (4)
                    if (errno == 4)
                    {
                        continue;
                    }

                    if (errno == 38) { D(b[(System.Int32)t..]); return; }
                    throw new System.InvalidOperationException("OS CSPRNG unavailable.");
                }

                t += (System.UIntPtr)r;
            }
        }
    }

    // -------------------- Apple: SecRandomCopyBytes --------------------

    [System.Runtime.InteropServices.LibraryImport(
        "/System/Library/Frameworks/Security.framework/Security", EntryPoint = "SecRandomCopyBytes")]
    private static partial System.Int32 SecRandomCopyBytes(System.IntPtr rnd, System.IntPtr count, System.IntPtr bytes);

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static unsafe void A(System.Span<System.Byte> b)
    {
        fixed (System.Byte* p = b)
        {
            System.Int32 s = SecRandomCopyBytes(System.IntPtr.Zero, b.Length, (System.IntPtr)p);
            if (s != 0)
            {
                D(b);
            }
        }
    }

    // -------------------- Fallback: /dev/urandom --------------------

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static void D(System.Span<System.Byte> b)
    {
        using System.IO.FileStream fs = new(
            "/dev/urandom",
            System.IO.FileMode.Open,
            System.IO.FileAccess.Read,
            System.IO.FileShare.Read
        );

        System.Int32 total = 0;
        while (total < b.Length)
        {
            System.Int32 r = fs.Read(b[total..]);
            if (r <= 0)
            {
                throw new System.InvalidOperationException("OS CSPRNG unavailable (/dev/urandom short read).");
            }

            total += r;
        }
    }

    #endregion Private
}
