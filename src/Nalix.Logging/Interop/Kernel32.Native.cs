// Copyright (c) 2025 PPN Corporation. All rights reserved.

namespace Nalix.Logging.Interop;

public static partial class Kernel32
{
    #region Getters

    [System.Diagnostics.StackTraceHidden]
    [System.Diagnostics.DebuggerNonUserCode]
    [System.Runtime.InteropServices.SuppressGCTransition]
    [System.Runtime.InteropServices.LibraryImport(KERNEL32, EntryPoint = "GetStdHandle")]
    [System.Runtime.InteropServices.DefaultDllImportSearchPaths(System.Runtime.InteropServices.DllImportSearchPath.System32)]
    [System.Runtime.InteropServices.UnmanagedCallConv(CallConvs = new[] { typeof(System.Runtime.CompilerServices.CallConvStdcall) })]
    private static partial System.IntPtr GET_STD_HANDLE(
        [System.Diagnostics.CodeAnalysis.NotNull] System.Int32 nStdHandle);

    [System.Diagnostics.StackTraceHidden]
    [System.Diagnostics.DebuggerNonUserCode]
    [System.Runtime.InteropServices.SuppressGCTransition]
    [System.Runtime.InteropServices.LibraryImport(KERNEL32, EntryPoint = "GetFileType")]
    [System.Runtime.InteropServices.DefaultDllImportSearchPaths(System.Runtime.InteropServices.DllImportSearchPath.System32)]
    [System.Runtime.InteropServices.UnmanagedCallConv(CallConvs = new[] { typeof(System.Runtime.CompilerServices.CallConvStdcall) })]
    private static partial System.UInt32 GET_FILE_TYPE(
        [System.Diagnostics.CodeAnalysis.NotNull] System.IntPtr hFile);

    [System.Diagnostics.StackTraceHidden]
    [System.Diagnostics.DebuggerNonUserCode]
    [System.Runtime.InteropServices.SuppressGCTransition]
    [System.Runtime.InteropServices.LibraryImport(KERNEL32, EntryPoint = "GetConsoleMode", SetLastError = true)]
    [System.Runtime.InteropServices.DefaultDllImportSearchPaths(System.Runtime.InteropServices.DllImportSearchPath.System32)]
    [System.Runtime.InteropServices.UnmanagedCallConv(CallConvs = new[] { typeof(System.Runtime.CompilerServices.CallConvStdcall) })]
    [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
    private static partial System.Boolean GET_CONSOLE_MODE(
        [System.Diagnostics.CodeAnalysis.NotNull] System.IntPtr hConsoleHandle,
        [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out System.UInt32 lpMode);

    [System.Diagnostics.StackTraceHidden]
    [System.Diagnostics.DebuggerNonUserCode]
    [System.Runtime.InteropServices.LibraryImport(KERNEL32, EntryPoint = "GetConsoleTitleW",
    SetLastError = true, StringMarshalling = System.Runtime.InteropServices.StringMarshalling.Utf16)]
    [System.Runtime.InteropServices.DefaultDllImportSearchPaths(System.Runtime.InteropServices.DllImportSearchPath.System32)]
    [System.Runtime.InteropServices.UnmanagedCallConv(CallConvs = new[] { typeof(System.Runtime.CompilerServices.CallConvStdcall) })]
    internal static unsafe partial System.UInt32 GET_CONSOLE_TITLE(
        [System.Diagnostics.CodeAnalysis.NotNull] System.Char* lpConsoleTitle,
        [System.Diagnostics.CodeAnalysis.NotNull] System.UInt32 nSize);

    [System.Diagnostics.StackTraceHidden]
    [System.Diagnostics.DebuggerNonUserCode]
    [System.Runtime.InteropServices.SuppressGCTransition]
    [System.Runtime.InteropServices.LibraryImport(KERNEL32, EntryPoint = "GetConsoleScreenBufferInfo", SetLastError = true)]
    [System.Runtime.InteropServices.DefaultDllImportSearchPaths(System.Runtime.InteropServices.DllImportSearchPath.System32)]
    [System.Runtime.InteropServices.UnmanagedCallConv(CallConvs = new[] { typeof(System.Runtime.CompilerServices.CallConvStdcall) })]
    [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
    private static partial System.Boolean GET_CONSOLE_SCREEN_BUFFER_INFO(
        [System.Diagnostics.CodeAnalysis.NotNull] System.IntPtr hConsoleOutput,
        [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out CONSOLE_SCREEN_BUFFER_INFO info);

    [System.Diagnostics.StackTraceHidden]
    [System.Diagnostics.DebuggerNonUserCode]
    [System.Runtime.InteropServices.SuppressGCTransition]
    [System.Runtime.InteropServices.LibraryImport(KERNEL32, EntryPoint = "GetCurrentConsoleFontEx", SetLastError = true)]
    [System.Runtime.InteropServices.DefaultDllImportSearchPaths(System.Runtime.InteropServices.DllImportSearchPath.System32)]
    [System.Runtime.InteropServices.UnmanagedCallConv(CallConvs = new[] { typeof(System.Runtime.CompilerServices.CallConvStdcall) })]
    [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
    private static partial System.Boolean GET_CURRENT_CONSOLE_FONT_EX(
        [System.Diagnostics.CodeAnalysis.NotNull] System.IntPtr hConsoleOutput,
        [System.Diagnostics.CodeAnalysis.NotNull]
        [System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)] System.Boolean maximumWindow,
        [System.Diagnostics.CodeAnalysis.NotNull] ref CONSOLE_FONT_INFOEX info);

    #endregion Getters

    #region Setters

    [System.Diagnostics.StackTraceHidden]
    [System.Diagnostics.DebuggerNonUserCode]
    [System.Runtime.InteropServices.SuppressGCTransition]
    [System.Runtime.InteropServices.LibraryImport(KERNEL32, EntryPoint = "SetConsoleMode", SetLastError = true)]
    [System.Runtime.InteropServices.DefaultDllImportSearchPaths(System.Runtime.InteropServices.DllImportSearchPath.System32)]
    [System.Runtime.InteropServices.UnmanagedCallConv(CallConvs = new[] { typeof(System.Runtime.CompilerServices.CallConvStdcall) })]
    [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
    internal static partial System.Boolean SET_CONSOLE_MODE(
        [System.Diagnostics.CodeAnalysis.NotNull] System.IntPtr hConsoleHandle,
        [System.Diagnostics.CodeAnalysis.NotNull] System.UInt32 dwMode);

    [System.Diagnostics.StackTraceHidden]
    [System.Diagnostics.DebuggerNonUserCode]
    [System.Runtime.InteropServices.LibraryImport(KERNEL32, EntryPoint = "SetConsoleTitleW",
    SetLastError = true, StringMarshalling = System.Runtime.InteropServices.StringMarshalling.Utf16)]
    [System.Runtime.InteropServices.DefaultDllImportSearchPaths(System.Runtime.InteropServices.DllImportSearchPath.System32)]
    [System.Runtime.InteropServices.UnmanagedCallConv(CallConvs = new[] { typeof(System.Runtime.CompilerServices.CallConvStdcall) })]
    [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
    internal static partial System.Boolean SET_CONSOLE_TITLE(
        [System.Diagnostics.CodeAnalysis.NotNull] System.String lpConsoleTitle);

    [System.Diagnostics.StackTraceHidden]
    [System.Diagnostics.DebuggerNonUserCode]
    [System.Runtime.InteropServices.SuppressGCTransition]
    [System.Runtime.InteropServices.LibraryImport(KERNEL32, EntryPoint = "SetConsoleScreenBufferSize", SetLastError = true)]
    [System.Runtime.InteropServices.DefaultDllImportSearchPaths(System.Runtime.InteropServices.DllImportSearchPath.System32)]
    [System.Runtime.InteropServices.UnmanagedCallConv(CallConvs = new[] { typeof(System.Runtime.CompilerServices.CallConvStdcall) })]
    [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
    private static partial System.Boolean SET_CONSOLE_SCREEN_BUFFER_SIZE(
        [System.Diagnostics.CodeAnalysis.NotNull] System.IntPtr hConsoleOutput,
        [System.Diagnostics.CodeAnalysis.NotNull] COORD size);

    [System.Diagnostics.StackTraceHidden]
    [System.Diagnostics.DebuggerNonUserCode]
    [System.Runtime.InteropServices.SuppressGCTransition]
    [System.Runtime.InteropServices.LibraryImport(KERNEL32, EntryPoint = "SetConsoleWindowInfo", SetLastError = true)]
    [System.Runtime.InteropServices.DefaultDllImportSearchPaths(System.Runtime.InteropServices.DllImportSearchPath.System32)]
    [System.Runtime.InteropServices.UnmanagedCallConv(CallConvs = new[] { typeof(System.Runtime.CompilerServices.CallConvStdcall) })]
    [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
    private static partial System.Boolean SET_CONSOLE_WINDOW_INFO(
        [System.Diagnostics.CodeAnalysis.NotNull] System.IntPtr hConsoleOutput,
        [System.Diagnostics.CodeAnalysis.NotNull]
        [System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)] System.Boolean absolute,
        [System.Diagnostics.CodeAnalysis.NotNull] in SMALL_RECT rect);

    [System.Diagnostics.StackTraceHidden]
    [System.Diagnostics.DebuggerNonUserCode]
    [System.Runtime.InteropServices.SuppressGCTransition]
    [System.Runtime.InteropServices.LibraryImport(KERNEL32, EntryPoint = "SetConsoleTextAttribute", SetLastError = true)]
    [System.Runtime.InteropServices.DefaultDllImportSearchPaths(System.Runtime.InteropServices.DllImportSearchPath.System32)]
    [System.Runtime.InteropServices.UnmanagedCallConv(CallConvs = new[] { typeof(System.Runtime.CompilerServices.CallConvStdcall) })]
    [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
    private static partial System.Boolean SET_CONSOLE_TEXT_ATTRIBUTE(
        [System.Diagnostics.CodeAnalysis.NotNull] System.IntPtr hConsoleOutput,
        [System.Diagnostics.CodeAnalysis.NotNull] System.UInt16 attributes);

    [System.Diagnostics.StackTraceHidden]
    [System.Diagnostics.DebuggerNonUserCode]
    [System.Runtime.InteropServices.LibraryImport(KERNEL32, EntryPoint = "SetCurrentConsoleFontEx", SetLastError = true)]
    [System.Runtime.InteropServices.DefaultDllImportSearchPaths(System.Runtime.InteropServices.DllImportSearchPath.System32)]
    [System.Runtime.InteropServices.UnmanagedCallConv(CallConvs = new[] { typeof(System.Runtime.CompilerServices.CallConvStdcall) })]
    [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
    private static partial System.Boolean SET_CURRENT_CONSOLE_FONT_EX(
        [System.Diagnostics.CodeAnalysis.NotNull] System.IntPtr hConsoleOutput,
        [System.Diagnostics.CodeAnalysis.NotNull]
        [System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)] System.Boolean maximumWindow,
        [System.Diagnostics.CodeAnalysis.NotNull] ref CONSOLE_FONT_INFOEX info);

    [System.Diagnostics.StackTraceHidden]
    [System.Diagnostics.DebuggerNonUserCode]
    [System.Runtime.InteropServices.LibraryImport(KERNEL32, EntryPoint = "GetConsoleCursorPosition", SetLastError = true)]
    [System.Runtime.InteropServices.DefaultDllImportSearchPaths(System.Runtime.InteropServices.DllImportSearchPath.System32)]
    [System.Runtime.InteropServices.UnmanagedCallConv(CallConvs = new[] { typeof(System.Runtime.CompilerServices.CallConvStdcall) })]
    [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
    internal static partial System.Boolean SET_CONSOLE_CURSOR_POSITION(
        [System.Diagnostics.CodeAnalysis.NotNull] System.IntPtr hConsoleOutput,
        [System.Diagnostics.CodeAnalysis.NotNull] COORD dwCursorPosition);

    #endregion Setters

    #region Fill

    [System.Diagnostics.StackTraceHidden]
    [System.Diagnostics.DebuggerNonUserCode]
    [System.Runtime.InteropServices.LibraryImport(KERNEL32, SetLastError = true, EntryPoint = "FillConsoleOutputCharacterW")]
    [System.Runtime.InteropServices.DefaultDllImportSearchPaths(System.Runtime.InteropServices.DllImportSearchPath.System32)]
    [System.Runtime.InteropServices.UnmanagedCallConv(CallConvs = new[] { typeof(System.Runtime.CompilerServices.CallConvStdcall) })]
    [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
    internal static partial System.Boolean FILL_CONSOLE_OUTPUT_CHARACTER_W(
        [System.Diagnostics.CodeAnalysis.NotNull] System.IntPtr hConsoleOutput,
        [System.Diagnostics.CodeAnalysis.NotNull] System.Char cCharacter,
        [System.Diagnostics.CodeAnalysis.NotNull] System.UInt32 nLength,
        [System.Diagnostics.CodeAnalysis.NotNull] COORD dwWriteCoord,
        [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out System.UInt32 lpNumberOfCharsWritten);

    [System.Diagnostics.StackTraceHidden]
    [System.Diagnostics.DebuggerNonUserCode]
    [System.Runtime.InteropServices.LibraryImport(KERNEL32, EntryPoint = "FillConsoleOutputAttribute", SetLastError = true)]
    [System.Runtime.InteropServices.DefaultDllImportSearchPaths(System.Runtime.InteropServices.DllImportSearchPath.System32)]
    [System.Runtime.InteropServices.UnmanagedCallConv(CallConvs = new[] { typeof(System.Runtime.CompilerServices.CallConvStdcall) })]
    [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
    internal static partial System.Boolean FILL_CONSOLE_OUTPUT_ATTRIBUTE(
        [System.Diagnostics.CodeAnalysis.NotNull] System.IntPtr hConsoleOutput,
        [System.Diagnostics.CodeAnalysis.NotNull] System.UInt16 wAttribute,
        [System.Diagnostics.CodeAnalysis.NotNull] System.UInt32 nLength,
        [System.Diagnostics.CodeAnalysis.NotNull] COORD dwWriteCoord,
        [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out System.UInt32 lpNumberOfAttrsWritten);

    #endregion Fill

    #region Console Allocation APIs

    [System.Diagnostics.StackTraceHidden]
    [System.Diagnostics.DebuggerNonUserCode]
    [System.Runtime.InteropServices.LibraryImport(
    KERNEL32, EntryPoint = "WriteConsoleW", SetLastError = true,
    StringMarshalling = System.Runtime.InteropServices.StringMarshalling.Utf16)]
    [System.Runtime.InteropServices.DefaultDllImportSearchPaths(System.Runtime.InteropServices.DllImportSearchPath.System32)]
    [System.Runtime.InteropServices.UnmanagedCallConv(CallConvs = new[] { typeof(System.Runtime.CompilerServices.CallConvStdcall) })]
    [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
    private static partial System.Boolean WRITE_CONSOLE_W(
    [System.Diagnostics.CodeAnalysis.NotNull] System.IntPtr hConsoleOutput,
    [System.Diagnostics.CodeAnalysis.NotNull] System.String lpBuffer,
    [System.Diagnostics.CodeAnalysis.NotNull] System.Int32 nNumberOfCharsToWrite,
    [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out System.Int32 lpNumberOfCharsWritten,
    [System.Diagnostics.CodeAnalysis.NotNull] System.IntPtr lpReserved);

    [System.Diagnostics.StackTraceHidden]
    [System.Diagnostics.DebuggerNonUserCode]
    [System.Runtime.InteropServices.LibraryImport(KERNEL32, EntryPoint = "AttachConsole", SetLastError = true)]
    [System.Runtime.InteropServices.DefaultDllImportSearchPaths(System.Runtime.InteropServices.DllImportSearchPath.System32)]
    [System.Runtime.InteropServices.UnmanagedCallConv(CallConvs = new[] { typeof(System.Runtime.CompilerServices.CallConvStdcall) })]
    [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
    private static partial System.Boolean ATTACH_CONSOLE(
        [System.Diagnostics.CodeAnalysis.MaybeNull] System.UInt32 dwProcessId);

    [System.Diagnostics.StackTraceHidden]
    [System.Diagnostics.DebuggerNonUserCode]
    [System.Runtime.InteropServices.LibraryImport(KERNEL32, EntryPoint = "AllocConsole", SetLastError = true)]
    [System.Runtime.InteropServices.DefaultDllImportSearchPaths(System.Runtime.InteropServices.DllImportSearchPath.System32)]
    [System.Runtime.InteropServices.UnmanagedCallConv(CallConvs = new[] { typeof(System.Runtime.CompilerServices.CallConvStdcall) })]
    [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
    private static partial System.Boolean ALLOC_CONSOLE();

    [System.Diagnostics.StackTraceHidden]
    [System.Diagnostics.DebuggerNonUserCode]
    [System.Runtime.InteropServices.LibraryImport(KERNEL32, EntryPoint = "FreeConsole", SetLastError = true)]
    [System.Runtime.InteropServices.DefaultDllImportSearchPaths(System.Runtime.InteropServices.DllImportSearchPath.System32)]
    [System.Runtime.InteropServices.UnmanagedCallConv(CallConvs = new[] { typeof(System.Runtime.CompilerServices.CallConvStdcall) })]
    [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
    private static partial System.Boolean FREE_CONSOLE();

    #endregion Console Allocation APIs
}
