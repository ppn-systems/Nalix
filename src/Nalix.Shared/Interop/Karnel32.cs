using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Nalix.Shared.Interop;

/// <summary>
/// Provides methods for interacting with the Windows Kernel32.dll and other related system functions.
/// </summary>
public static partial class Karnel32
{
    #region 🔴 Constants

    private const int STD_OUTPUT_HANDLE = -11;
    private const int SW_HIDE = 0;
    private const int SW_SHOW = 5;
    private const int MAX_TITLE_LENGTH = 256;
    private const uint ATTACH_PARENT_PROCESS = 0xFFFFFFFF;

    #endregion 🔴 Constants

    #region 🟢 Public API

    #region 🔍 Debugger Detection

    /// <summary>
    /// Checks if the calling process is being debugged.
    /// </summary>
    [LibraryImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool IsDebuggerPresent();

    /// <summary>
    /// Determines whether the current process is being debugged.
    /// </summary>
    public static bool IsBeingDebugged()
    {
        int isDebugged = 0;
        _ = NtQueryInformationProcess(
            Process.GetCurrentProcess().Handle, 7, ref isDebugged, sizeof(int), out _);
        return isDebugged != 0;
    }

    #endregion 🔍 Debugger Detection

    #region 🎛 Console Management

    /// <summary>
    /// Hides the console window.
    /// </summary>
    public static void HideConsole()
    {
        IntPtr consoleHandle = GetConsoleWindow();
        if (consoleHandle != IntPtr.Zero) ShowWindow(consoleHandle, SW_HIDE);
    }

    /// <summary>
    /// Shows the console window.
    /// </summary>
    public static void ShowConsole()
    {
        IntPtr consoleHandle = GetConsoleWindow();
        if (consoleHandle != IntPtr.Zero) ShowWindow(consoleHandle, SW_SHOW);
    }

    /// <summary>
    /// Checks if the process has an associated console.
    /// </summary>
    public static bool HasConsole()
        => GetConsoleWindow() != IntPtr.Zero;

    /// <summary>
    /// Allocates a new console for the calling process.
    /// </summary>
    public static void AllocateConsole()
    {
        if (HasConsole()) return;
        AllocConsole();
    }

    /// <summary>
    /// Frees the current process's console.
    /// </summary>
    public static void FreeConsole()
    {
        if (!HasConsole()) return;
        FreeConsoleNative();
    }

    /// <summary>
    /// Determines whether the process can show a console window.
    /// </summary>
    public static bool CanShowConsole()
        => GetConsoleWindow() != IntPtr.Zero || AttachConsole(ATTACH_PARENT_PROCESS);

    #endregion 🎛 Console Management

    #region 🏷 Console Window Properties

    /// <summary>
    /// Gets the title of the console window.
    /// </summary>
    public static string GetConsoleTitle()
    {
        char[] title = new char[MAX_TITLE_LENGTH];
        int length = GetConsoleTitle(title, MAX_TITLE_LENGTH);
        return length > 0 ? new string(title, 0, length) : string.Empty;
    }

    /// <summary>
    /// Sets the title of the console window.
    /// </summary>
    public static void SetConsoleTitle(string title)
        => SetConsoleTitleNative(title);

    /// <summary>
    /// Sets the size of the console screen buffer.
    /// </summary>
    public static void SetConsoleBufferSize(int width, int height)
    {
        IntPtr hConsole = GetStdHandle(STD_OUTPUT_HANDLE);
        if (hConsole == IntPtr.Zero) return;

        Coord size = new((short)width, (short)height);
        SetConsoleScreenBufferSize(hConsole, size);
    }

    /// <summary>
    /// Sets the size of the console window.
    /// </summary>
    public static void SetConsoleSize(int width, int height)
    {
        SetConsoleBufferSize(width, height);
        SmallRect rect = new(0, 0, (short)(width - 1), (short)(height - 1));
        IntPtr hConsole = GetStdHandle(STD_OUTPUT_HANDLE);
        if (hConsole != IntPtr.Zero)
            SetConsoleWindowInfo(hConsole, true, ref rect);
    }

    #endregion 🏷 Console Window Properties

    #region ⌨ Console Input/Output

    /// <summary>
    /// Writes a message to the console using the native Windows API.
    /// </summary>
    public static void WriteToConsole(string message)
    {
        if (!HasConsole()) return;

        IntPtr hConsole = GetStdHandle(STD_OUTPUT_HANDLE);
        if (hConsole == IntPtr.Zero || !GetConsoleMode(hConsole, out _))
            return;

        WriteConsole(hConsole, message, (uint)message.Length, out _, IntPtr.Zero);
    }

    #endregion ⌨ Console Input/Output

    #endregion 🟢 Public API

    #region 🔴 Private API

    #region 🔗 WinAPI Imports

    [LibraryImport("kernel32.dll", SetLastError = true)]
    private static partial IntPtr GetStdHandle(int nStdHandle);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    private static partial IntPtr GetConsoleWindow();

    [LibraryImport("kernel32.dll", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    private static partial int GetConsoleTitle([MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 1), In, Out] char[] lpConsoleTitle, int nSize);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool AllocConsole();

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool FreeConsoleNative();

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool AttachConsole(uint dwProcessId);

    [LibraryImport("kernel32.dll", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetConsoleTitleNative(string lpConsoleTitle);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetConsoleScreenBufferSize(IntPtr hConsoleOutput, Coord size);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetConsoleWindowInfo(
        IntPtr hConsoleOutput,
        [MarshalAs(UnmanagedType.Bool)] bool absolute, ref
        SmallRect consoleWindow);

    /// <summary>
    /// Retrieves the current input mode of a console's input buffer.
    /// </summary>
    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetConsoleMode(IntPtr hConsoleHandle, out uint lpMode);

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [LibraryImport("kernel32.dll", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool WriteConsole(
        IntPtr hConsoleOutput,
        [MarshalAs(UnmanagedType.LPWStr)] string lpBuffer,
        uint nNumberOfCharsToWrite,
        out uint lpNumberOfCharsWritten,
        IntPtr lpReserved);

    #endregion 🔗 WinAPI Imports

    #region 🔗 Ntdll Imports

    [LibraryImport("ntdll.dll", SetLastError = true)]
    private static partial int NtQueryInformationProcess(
        nint processHandle,
        int processInformationClass,
        ref int processInformation,
        int processInformationLength,
        out int returnLength);

    #endregion 🔗 Ntdll Imports

    #region 📏 Structs & Constants

    [StructLayout(LayoutKind.Sequential)]
    internal struct SmallRect(short left, short top, short right, short bottom)
    {
        public short Left = left;
        public short Top = top;
        public short Right = right;
        public short Bottom = bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct Coord(short x, short y)
    {
        public short X = x;
        public short Y = y;
    }

    #endregion 📏 Structs & Constants

    #endregion 🔴 Private API
}
