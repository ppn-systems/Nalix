// Copyright (c) 2025 PPN Corporation. All rights reserved.

namespace Nalix.Logging.Interop;

public static partial class Kernel32
{
    internal const System.String KERNEL32 = "kernel32.dll";
    internal const System.Int32 LF_FACESIZE = 32;

    internal const System.Int32 STD_INPUT_HANDLE = -10;
    internal const System.Int32 STD_OUTPUT_HANDLE = -11;
    internal const System.Int32 STD_ERROR_HANDLE = -12;

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
}
