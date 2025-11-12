// Copyright (c) 2025 PPN Corporation. All rights reserved.

namespace Nalix.Logging.Interop;

internal static partial class Kernel32
{
    internal const System.String KERNEL32 = "kernel32.dll";
    internal const System.Int32 LF_FACESIZE = 32;

    internal const System.Int32 STD_INPUT_HANDLE = -10;
    internal const System.Int32 STD_OUTPUT_HANDLE = -11;
    internal const System.Int32 STD_ERROR_HANDLE = -12;

    internal const System.UInt32 OPEN_EXISTING = 3;
    internal const System.UInt32 GENERIC_READ = 0x80000000;
    internal const System.UInt32 GENERIC_WRITE = 0x40000000;
    internal const System.UInt32 FILE_SHARE_READ = 0x00000001;
    internal const System.UInt32 FILE_SHARE_WRITE = 0x00000002;
    internal const System.UInt32 CREATE_NEW_CONSOLE = 0x00000010;
    internal const System.UInt32 ENABLE_PROCESSED_OUTPUT = 0x0001;
    internal const System.UInt32 ENABLE_WRAP_AT_EOL_OUTPUT = 0x0002;
    internal const System.UInt32 ATTACH_PARENT_PROCESS = 0xFFFFFFFF;
    internal const System.UInt32 ENABLE_VIRTUAL_TERMINAL_PROCESSING = 0x0004;

    [System.Flags]
    internal enum FileType : System.UInt32
    {
        Unknown = 0x0000,
        Disk = 0x0001,
        Char = 0x0002,
        Pipe = Disk | Char,
        Remote = 0x8000
    }

    // ============================================================= //
    // STRUCT: COORD
    // ============================================================= //
    [System.Runtime.InteropServices.StructLayout(
        System.Runtime.InteropServices.LayoutKind.Sequential, Pack = 1)]
    internal struct COORD
    {
        public System.Int16 X;
        public System.Int16 Y;

        [System.Diagnostics.CodeAnalysis.SuppressMessage(
            "Style", "IDE0290:Use primary constructor", Justification = "<Pending>")]
        public COORD(System.Int16 x, System.Int16 y)
        {
            X = x;
            Y = y;
        }
    }

    // ============================================================= //
    // STRUCT: SMALL_RECT
    // ============================================================= //
    [System.Runtime.InteropServices.StructLayout(
        System.Runtime.InteropServices.LayoutKind.Sequential, Pack = 1)]
    internal struct SMALL_RECT
    {
        public System.Int16 Left;
        public System.Int16 Top;
        public System.Int16 Right;
        public System.Int16 Bottom;

        [System.Diagnostics.CodeAnalysis.SuppressMessage(
            "Style", "IDE0290:Use primary constructor", Justification = "<Pending>")]
        public SMALL_RECT(System.Int16 left, System.Int16 top, System.Int16 right, System.Int16 bottom)
        {
            Left = left;
            Top = top;
            Right = right;
            Bottom = bottom;
        }
    }

    // ============================================================= //
    // STRUCT: CONSOLE_SCREEN_BUFFER_INFO
    // ============================================================= //
    [System.Runtime.InteropServices.StructLayout(
        System.Runtime.InteropServices.LayoutKind.Sequential, Pack = 1)]
    internal struct CONSOLE_SCREEN_BUFFER_INFO
    {
        public SMALL_RECT srWindow;
        public System.UInt16 wAttributes;

        public COORD dwSize;
        public COORD dwCursorPosition;
        public COORD dwMaximumWindowSize;
    }

    [System.Runtime.InteropServices.StructLayout(
        System.Runtime.InteropServices.LayoutKind.Sequential,
        CharSet = System.Runtime.InteropServices.CharSet.Unicode, Pack = 1)]
    internal unsafe struct CONSOLE_FONT_INFOEX
    {
        public COORD dwFontSize;  // pixel size

        public System.UInt32 nFont;
        public System.UInt32 cbSize;
        public System.UInt32 FontFamily;
        public System.UInt32 FontWeight;
        public fixed System.Char FaceName[LF_FACESIZE];
    }

    [System.Runtime.InteropServices.StructLayout(
        System.Runtime.InteropServices.LayoutKind.Sequential, Pack = 1)]
    internal struct STARTUPINFOW
    {
        public System.UInt32 cb;
        public System.IntPtr lpReserved;
        public System.IntPtr lpDesktop;
        public System.IntPtr lpTitle;
        public System.UInt32 dwX;
        public System.UInt32 dwY;
        public System.UInt32 dwXSize;
        public System.UInt32 dwYSize;
        public System.UInt32 dwXCountChars;
        public System.UInt32 dwYCountChars;
        public System.UInt32 dwFillAttribute;
        public System.UInt32 dwFlags;
        public System.UInt16 wShowWindow;
        public System.UInt16 cbReserved2;
        public System.IntPtr lpReserved2;
        public System.IntPtr hStdInput;
        public System.IntPtr hStdOutput;
        public System.IntPtr hStdError;
    }

    [System.Runtime.InteropServices.StructLayout(
        System.Runtime.InteropServices.LayoutKind.Sequential, Pack = 1)]
    internal struct PROCESS_INFORMATION
    {
        public System.IntPtr hProcess;
        public System.IntPtr hThread;
        public System.UInt32 dwProcessId;
        public System.UInt32 dwThreadId;
    }

    /// <summary>
    /// Returns a valid handle to the standard output console, or IntPtr.Zero if not a console.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    internal static System.IntPtr GET_STD_OUT()
    {
        System.IntPtr h = GET_STD_HANDLE(STD_OUTPUT_HANDLE);
        if (h is 0 or (-1))
        {
            return System.IntPtr.Zero;
        }

        // Ensure it's a console/character device
        var ft = (FileType)GET_FILE_TYPE(h);
        if (ft is not FileType.Char and not FileType.Unknown)
        {
            return System.IntPtr.Zero;
        }

        // Make sure console mode can be retrieved
        if (!GET_CONSOLE_MODE(h, out _))
        {
            return System.IntPtr.Zero;
        }

        return h;
    }

    /// <summary>
    /// Sets console screen buffer size (columns, rows).
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static System.Boolean SET_BUFFER_SIZE(System.Int16 columns, System.Int16 rows)
    {
        System.IntPtr h = GET_STD_OUT();
        if (h == System.IntPtr.Zero)
        {
            return false;
        }

        return SET_CONSOLE_SCREEN_BUFFER_SIZE(h, new COORD(columns, rows));
    }

    /// <summary>
    /// Sets visible console window size (columns, rows). Adjusts buffer if needed.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    public static System.Boolean SET_WINDOW_SIZE(System.Int16 columns, System.Int16 rows)
    {
        System.IntPtr h = GET_STD_OUT();
        if (h == System.IntPtr.Zero)
        {
            return false;
        }

        if (!GET_CONSOLE_SCREEN_BUFFER_INFO(h, out CONSOLE_SCREEN_BUFFER_INFO info))
        {
            return false;
        }

        // Ensure buffer is at least as big as requested window
        System.Int16 bufCols = System.Math.Max(info.dwSize.X, columns);
        System.Int16 bufRows = System.Math.Max(info.dwSize.Y, rows);
        if (!SET_CONSOLE_SCREEN_BUFFER_SIZE(h, new COORD(bufCols, bufRows)))
        {
            // some consoles require window shrink-before-grow dance; try shrink window first
            SMALL_RECT shrunk = new(0, 0, (System.Int16)System.Math.Max(0, System.Math.Min(info.srWindow.Left + columns - 1, bufCols - 1)),
                                          (System.Int16)System.Math.Max(0, System.Math.Min(info.srWindow.Top + rows - 1, bufRows - 1)));

            _ = SET_CONSOLE_WINDOW_INFO(h, true, in shrunk);
            if (!SET_CONSOLE_SCREEN_BUFFER_SIZE(h, new COORD(bufCols, bufRows)))
            {
                return false;
            }
        }

        // Now set window rect (0,0)-(cols-1, rows-1)
        SMALL_RECT rect = new(0, 0, (System.Int16)(columns - 1), (System.Int16)(rows - 1));
        return SET_CONSOLE_WINDOW_INFO(h, true, in rect);
    }
}
