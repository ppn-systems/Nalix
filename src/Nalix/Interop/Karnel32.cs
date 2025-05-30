using Nalix.Interop.Internal;
using System.Runtime.InteropServices;

namespace Nalix.Interop;

/// <summary>
/// Provides methods for interacting with the Windows Kernel32.dll and other related system functions.
/// </summary>
public static partial class Kernel32
{
    #region 🔴 Constants

    private const System.Int32 STD_OUTPUT_HANDLE = -11;
    private const System.Int32 SW_HIDE = 0;
    private const System.Int32 SW_SHOW = 5;
    private const System.Int32 MAX_TITLE_LENGTH = 256;
    private const System.UInt32 ATTACH_PARENT_PROCESS = 0xFFFFFFFF;

    #endregion 🔴 Constants

    #region 🟢 Public API

    #region 🔍 Debugger Detection

    /// <summary>
    /// Checks if the calling process is being debugged.
    /// </summary>
    [LibraryImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial System.Boolean IsDebuggerPresent();

    /// <summary>
    /// Determines whether the current process is being debugged.
    /// </summary>
    public static System.Boolean IsBeingDebugged()
    {
        int isDebugged = 0;
        _ = NtQueryInformationProcess(
            System.Diagnostics.Process.GetCurrentProcess().Handle,
            7, ref isDebugged, sizeof(System.Int32), out _);

        return isDebugged != 0;
    }

    #endregion 🔍 Debugger Detection

    #region 🎛 Console Management

    /// <summary>
    /// Hides the console window.
    /// </summary>
    public static void HideConsole()
    {
        System.IntPtr consoleHandle = GetConsoleWindow();
        if (consoleHandle != System.IntPtr.Zero) ShowWindow(consoleHandle, SW_HIDE);
    }

    /// <summary>
    /// Shows the console window.
    /// </summary>
    public static void ShowConsole()
    {
        System.IntPtr consoleHandle = GetConsoleWindow();
        if (consoleHandle != System.IntPtr.Zero) ShowWindow(consoleHandle, SW_SHOW);
    }

    /// <summary>
    /// Checks if the process has an associated console.
    /// </summary>
    public static System.Boolean HasConsole()
        => GetConsoleWindow() != System.IntPtr.Zero;

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
    public static System.Boolean CanShowConsole()
        => GetConsoleWindow() != System.IntPtr.Zero || AttachConsole(ATTACH_PARENT_PROCESS);

    #endregion 🎛 Console Management

    #region 🏷 Console Window Properties

    /// <summary>
    /// Gets the title of the console window.
    /// </summary>
    public static string GetConsoleTitle()
    {
        System.Char[] title = new System.Char[MAX_TITLE_LENGTH];
        System.Int32 length = GetConsoleTitle(title, MAX_TITLE_LENGTH);
        return length > 0 ? new System.String(title, 0, length) : System.String.Empty;
    }

    /// <summary>
    /// Sets the title of the console window.
    /// </summary>
    public static void SetConsoleTitle(System.String title)
        => SetConsoleTitleNative(title);

    /// <summary>
    /// Sets the size of the console screen buffer.
    /// </summary>
    public static void SetConsoleBufferSize(System.Int32 width, System.Int32 height)
    {
        System.IntPtr hConsole = GetStdHandle(STD_OUTPUT_HANDLE);
        if (hConsole == System.IntPtr.Zero) return;

        Coord size = new((System.Int16)width, (System.Int16)height);
        SetConsoleScreenBufferSize(hConsole, size);
    }

    /// <summary>
    /// Sets the size of the console window.
    /// </summary>
    public static void SetConsoleSize(System.Int32 width, System.Int32 height)
    {
        SetConsoleBufferSize(width, height);
        SmallRect rect = new(0, 0, (System.Int16)(width - 1), (System.Int16)(height - 1));
        System.IntPtr hConsole = GetStdHandle(STD_OUTPUT_HANDLE);
        if (hConsole != System.IntPtr.Zero)
        {
            SetConsoleWindowInfo(hConsole, true, ref rect);
        }
    }

    #endregion 🏷 Console Window Properties

    #region ⌨ Console Input/Output

    /// <summary>
    /// Writes a message to the console using the native Windows API.
    /// </summary>
    public static void WriteToConsole(System.String message)
    {
        if (!HasConsole()) return;

        System.IntPtr hConsole = GetStdHandle(STD_OUTPUT_HANDLE);
        if (hConsole == System.IntPtr.Zero || !GetConsoleMode(hConsole, out _))
            return;

        WriteConsole(hConsole, message, (System.UInt32)message.Length, out _, System.IntPtr.Zero);
    }

    #endregion ⌨ Console Input/Output

    #endregion 🟢 Public API

    #region 🔴 Private API

    #region 🔗 WinAPI Imports

    [LibraryImport("kernel32.dll", SetLastError = true)]
    private static partial System.IntPtr GetStdHandle(System.Int32 nStdHandle);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    private static partial System.IntPtr GetConsoleWindow();

    [LibraryImport("kernel32.dll", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    private static partial System.Int32 GetConsoleTitle(
        [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 1), In, Out]
        System.Char[] lpConsoleTitle, System.Int32 nSize);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial System.Boolean AllocConsole();

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial System.Boolean FreeConsoleNative();

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial System.Boolean AttachConsole(System.UInt32 dwProcessId);

    [LibraryImport("kernel32.dll", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial System.Boolean SetConsoleTitleNative(System.String lpConsoleTitle);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial System.Boolean SetConsoleScreenBufferSize(
        System.IntPtr hConsoleOutput, Coord size);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial System.Boolean SetConsoleWindowInfo(
        System.IntPtr hConsoleOutput,
        [MarshalAs(UnmanagedType.Bool)] System.Boolean absolute,
        ref SmallRect consoleWindow);

    /// <summary>
    /// Retrieves the current input mode of a console's input buffer.
    /// </summary>
    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial System.Boolean GetConsoleMode(
        System.IntPtr hConsoleHandle, out System.UInt32 lpMode);

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial System.Boolean ShowWindow(
        System.IntPtr hWnd, System.Int32 nCmdShow);

    [LibraryImport("kernel32.dll", SetLastError = true, StringMarshalling = StringMarshalling.Utf8)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial System.Boolean WriteConsole(
        System.IntPtr hConsoleOutput,
        [MarshalAs(UnmanagedType.LPWStr)] System.String lpBuffer,
        System.UInt32 nNumberOfCharsToWrite,
        out System.UInt32 lpNumberOfCharsWritten,
        System.IntPtr lpReserved);

    #endregion 🔗 WinAPI Imports

    #region 🔗 Ntdll Imports

    [LibraryImport("ntdll.dll", SetLastError = true)]
    private static partial System.Int32 NtQueryInformationProcess(
        System.IntPtr processHandle,
        System.Int32 processInformationClass,
        ref System.Int32 processInformation,
        System.Int32 processInformationLength,
        out System.Int32 returnLength);

    #endregion 🔗 Ntdll Imports

    #endregion 🔴 Private API
}
