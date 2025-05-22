// Copyright (c) 2025 PPN Corporation. All rights reserved.

[assembly: System.Runtime.CompilerServices.DisableRuntimeMarshalling]

namespace Nalix.Logging.Interop;

/// <summary>
/// Kernel32.dll API wrappers and helper methods.
/// </summary>
[System.Diagnostics.DebuggerNonUserCode]
[System.Runtime.Versioning.SupportedOSPlatform("windows")]
public static partial class Kernel32
{
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
    /// Writes text using WRITE_CONSOLE_W. Returns false if not a console or on error.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    public static System.Boolean WRITE(System.String text)
    {
        var h = GET_STD_OUT();
        if (h == System.IntPtr.Zero)
        {
            return false;
        }

        if (!WRITE_CONSOLE_W(h, text, text.Length, out _, System.IntPtr.Zero))
        {
            _ = System.Runtime.InteropServices.Marshal.GetLastPInvokeError();
            return false;
        }
        return true;
    }

    /// <summary>
    /// Writes a line (text + CRLF) using WRITE_CONSOLE_W.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static System.Boolean WRITE_LINE(System.String text)
    {
        // Windows console treats \r\n as newline. Ensure CRLF.
        System.String value = text + "\r\n";
        return WRITE(value);
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

    /// <summary>
    /// Sets console text attributes (foreground/background colors, intensity, etc.)
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static System.Boolean SET_TEXT_ATTRIBUTES(System.UInt16 attributes)
    {
        System.IntPtr h = GET_STD_OUT();
        if (h == System.IntPtr.Zero)
        {
            return false;
        }

        return SET_CONSOLE_TEXT_ATTRIBUTE(h, attributes);
    }

    /// <summary>
    /// Sets the console window title.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    public static System.Boolean SET_TITLE(System.String title)
    {
        System.IntPtr h = GET_STD_HANDLE(STD_OUTPUT_HANDLE);
        if (h == System.IntPtr.Zero || h == (System.IntPtr)(-1))
        {
            return false;
        }

        FileType ft = (FileType)GET_FILE_TYPE(h);
        if (ft is not FileType.Char and not FileType.Unknown)
        {
            return false;
        }

        return SET_CONSOLE_TITLE(title);
    }

    /// <summary>
    /// Ensures a console is attached. Tries to attach to parent process console,
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    public static System.Boolean ENSURE_CONSOLE()
    {
        if (ATTACH_CONSOLE(ATTACH_PARENT_PROCESS))
        {
            return true;
        }

        return ALLOC_CONSOLE();
    }

    /// <summary>
    /// Ensures a console is attached and sets the console title.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static System.Boolean ENSURE_CONSOLE_AND_SET_TITLE(System.String title)
    {
        if (!ENSURE_CONSOLE())
        {
            return false;
        }

        return SET_CONSOLE_TITLE(title);
    }

    /// <summary>
    /// Closes the current console attached to the process.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static void CLOSE_CONSOLE() => FREE_CONSOLE();

    /// <summary>
    /// Enables ANSI escape code processing on the console output.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    public static System.Boolean ENABLE_ANSI()
    {
        var h = GET_STD_OUT();
        if (h == System.IntPtr.Zero)
        {
            return false;
        }

        if (!GET_CONSOLE_MODE(h, out System.UInt32 mode))
        {
            return false;
        }

        System.UInt32 desired = mode | ENABLE_VIRTUAL_TERMINAL_PROCESSING | ENABLE_PROCESSED_OUTPUT | ENABLE_WRAP_AT_EOL_OUTPUT;
        return SET_CONSOLE_MODE(h, desired);
    }

    /// <summary>
    /// Clears the entire screen buffer and moves the cursor to (0,0).
    /// Uses native Win32 to work even if ANSI is disabled.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    public static System.Boolean CLEAR()
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

        // Total cells = buffer width * height
        // Use uint to avoid sign issues when multiplying
        System.UInt32 cells = (System.UInt32)(info.dwSize.X) * (System.UInt32)(info.dwSize.Y);

        // 1) Fill characters with space
        if (!FILL_CONSOLE_OUTPUT_CHARACTER_W(h, ' ', cells, new COORD(0, 0), out _))
        {
            return false;
        }

        // 2) Reset attributes to current attributes (keeps colors consistent)
        if (!FILL_CONSOLE_OUTPUT_ATTRIBUTE(h, info.wAttributes, cells, new COORD(0, 0), out _))
        {
            return false;
        }

        // 3) Move cursor home
        return SET_CONSOLE_CURSOR_POSITION(h, new COORD(0, 0));
    }
}