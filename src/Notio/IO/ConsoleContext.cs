using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace Notio.IO;

/// <summary>
/// Provides a context for managing console input and output, 
/// including features such as input history, buffered input handling, 
/// and asynchronous cursor management.
/// </summary>
public sealed partial class ConsoleContext
{
    #region Fields

    /// <summary>
    /// Default length of the input history.
    /// </summary>
    public static readonly int DefaultInputHistoryLength = 100;

    private readonly List<string> _inputHistory;
    private readonly Lock _lock;
    private bool _inputHistoryEnabled;
    private int _inputHistoryLength;
    private int _inputHistoryPosition;

    #endregion

    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="ConsoleContext"/> class.
    /// </summary>
    /// <param name="inputHistoryLength">The length of the input history buffer. Default is 0.</param>
    public ConsoleContext(int inputHistoryLength = 0)
    {
        _lock = new Lock();
        _inputHistory = new List<string>(Math.Max(0, inputHistoryLength));
        InputHistoryLength = inputHistoryLength;
        InputBuffer = new List<char>(80);
    }

    #endregion

    #region Properties

    /// <summary>
    /// Gets or sets the position of the cursor while waiting for input.
    /// </summary>
    private int WaitCursorLeft { get; set; }

    /// <summary>
    /// Gets or sets the top position of the cursor while waiting for input.
    /// </summary>
    private int WaitCursorTop { get; set; }

    /// <summary>
    /// Gets a value indicating whether the console is currently waiting for a read operation.
    /// </summary>
    public bool IsWaitingRead { get; private set; }

    /// <summary>
    /// Gets or sets the current position in the input history.
    /// </summary>
    public int InputHistoryPosition
    {
        get => _inputHistoryPosition;
        set => _inputHistoryPosition = Math.Max(0, Math.Min(_inputHistory.Count, value));
    }

    /// <summary>
    /// Gets or sets a value indicating whether input history is enabled.
    /// </summary>
    public bool InputHistoryEnabled
    {
        get => _inputHistoryEnabled;
        set
        {
            _inputHistoryEnabled = value;
            _inputHistoryLength = _inputHistoryEnabled ? DefaultInputHistoryLength : 0;
        }
    }

    /// <summary>
    /// Gets or sets the maximum length of the input history.
    /// </summary>
    public int InputHistoryLength
    {
        get => _inputHistoryLength;
        set
        {
            _inputHistoryLength = value;
            _inputHistoryEnabled = _inputHistoryLength != 0;
        }
    }

    /// <summary>
    /// Gets a value indicating whether the input history is full.
    /// </summary>
    public bool InputHistoryFull => _inputHistoryLength > 0 && _inputHistoryLength <= _inputHistory.Count;

    /// <summary>
    /// Gets the input history as an immutable list.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown if the input history cannot be accessed.</exception>
    public ImmutableList<string> InputHistory =>
        _inputHistory.ToImmutableList() ?? throw new InvalidOperationException();

    /// <summary>
    /// Gets or sets the prefix displayed while waiting for input.
    /// </summary>
    public string WaitPrefix { get; set; }

    /// <summary>
    /// Gets the buffer for the current input.
    /// </summary>
    private List<char> InputBuffer { get; }

    #endregion

    /// <summary>
    /// Checks if the console is already waiting on a read operation.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown if the console is already waiting for a read operation.
    /// </exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Check()
    {
        if (IsWaitingRead)
        {
            throw new InvalidOperationException("Already waiting on a Read*() operation.");
        }
    }

    /// <summary>
    /// Clears the current console state, resetting the waiting status.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Clear()
    {
        lock (_lock)
        {
            IsWaitingRead = false;
        }
    }

    /// <summary>
    /// Waits for a result while writing a prefix to the console, ensuring thread safety.
    /// </summary>
    /// <typeparam name="TResult">The type of the result to be returned.</typeparam>
    /// <param name="write">The action used to write the prefix to the console.</param>
    /// <param name="waitForResult">The function that provides the result.</param>
    /// <returns>The result of the <paramref name="waitForResult"/> function.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the console is already in a read operation.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TResult Wait<TResult>(Action<string> write, Func<TResult> waitForResult)
    {
        lock (_lock)
        {
            IsWaitingRead = true;
            WritePrefix(write);
        }

        return waitForResult();
    }

    /// <summary>
    /// Resets the cursor position and clears the buffer synchronously.
    /// </summary>
    /// <param name="write">The action used to write to the console.</param>
    /// <param name="clearBufferLength">
    /// The length of the buffer to clear. Default value is -1, which clears the current input buffer.
    /// </param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void ResetWaitCursor(Action<string> write, int clearBufferLength = -1)
    {
        if (!IsWaitingRead)
        {
            return;
        }

        var onCurrentLine = Console.CursorTop == WaitCursorTop;
        var realColumnCursor = onCurrentLine ? WaitCursorLeft : 0;
        var currentLineCursor = Console.CursorTop;

        Console.SetCursorPosition(realColumnCursor, Console.CursorTop);
        write(new string(' ', (WaitPrefix?.Length ?? 0) + Math.Max(clearBufferLength, InputBuffer.Count)));
        Console.SetCursorPosition(realColumnCursor, currentLineCursor);

        if (onCurrentLine)
        {
            return;
        }

        Console.CursorLeft = WaitCursorLeft;
        Console.CursorTop = WaitCursorTop;
    }

    /// <summary>
    /// Resets the cursor position and clears the buffer asynchronously.
    /// </summary>
    /// <param name="writeAsync">The asynchronous function used to write to the console.</param>
    /// <param name="clearBufferLength">
    /// The length of the buffer to clear. Default value is -1, which clears the current input buffer.
    /// </param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task ResetWaitCursorAsync(Func<string, Task> writeAsync, int clearBufferLength = -1)
    {
        if (!IsWaitingRead)
        {
            return;
        }

        var onCurrentLine = Console.CursorTop == WaitCursorTop;

        var realColumnCursor = onCurrentLine ? WaitCursorLeft : 0;
        var currentLineCursor = Console.CursorTop;
        Console.SetCursorPosition(realColumnCursor, Console.CursorTop);

        var task = writeAsync(
            new string(' ', (WaitPrefix?.Length ?? 0) + Math.Max(clearBufferLength, InputBuffer.Count))
        );

        if (task != null)
        {
            await task;
        }

        Console.SetCursorPosition(realColumnCursor, currentLineCursor);

        if (onCurrentLine)
        {
            return;
        }

        Console.CursorLeft = WaitCursorLeft;
        Console.CursorTop = WaitCursorTop;
    }

    /// <summary>
    /// Updates the cursor position for waiting operations.
    /// </summary>
    /// <param name="subtractPrefix">
    /// If true, adjusts the cursor position to account for the length of the prefix.
    /// </param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void MoveWaitCursor(bool subtractPrefix = false)
    {
        WaitCursorTop = Console.CursorTop;
        WaitCursorLeft = Console.CursorLeft;

        if (subtractPrefix)
        {
            WaitCursorLeft -= WaitPrefix?.Length ?? 0;
        }
    }

    /// <summary>
    /// Writes a prefix to the console and adjusts the cursor position.
    /// </summary>
    /// <param name="write">The action used to write the prefix to the console.</param>
    /// <param name="bufferPosition">
    /// The position in the buffer to adjust the cursor to. Default is -1, meaning no adjustment.
    /// </param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WritePrefix(Action<string> write, int bufferPosition = -1)
    {
        if (!IsWaitingRead)
        {
            return;
        }

        MoveWaitCursor();

        if (string.IsNullOrEmpty(WaitPrefix))
        {
            return;
        }

        write(WaitPrefix + new string([.. InputBuffer]));
        if (bufferPosition > -1)
        {
            Console.CursorLeft -= Math.Max(0, InputBuffer.Count - bufferPosition);
        }
    }

    /// <summary>
    /// Writes a prefix to the console asynchronously.
    /// </summary>
    /// <param name="writeAsync">The asynchronous function used to write the prefix to the console.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task WritePrefixAsync(Func<string, Task> writeAsync)
    {
        if (!IsWaitingRead)
        {
            return;
        }

        MoveWaitCursor();

        if (string.IsNullOrEmpty(WaitPrefix))
        {
            return;
        }

        var task = writeAsync(WaitPrefix);
        if (task != null)
        {
            await task;
        }
    }

    /// <summary>
    /// Reads a line of input from the console with buffered handling and cursor adjustments.
    /// </summary>
    /// <param name="write">The action used to write to the console.</param>
    /// <param name="writeLine">The action used to write a new line to the console.</param>
    /// <param name="readKey">The function used to read a key input from the console.</param>
    /// <returns>The input line entered by the user.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public string BufferedReadLine(Action<string> write, Action writeLine, Func<ConsoleKeyInfo> readKey)
    {
        return Wait(
            write, () =>
            {
                InputBuffer.Clear();

                var line = string.Empty;
                var finished = false;
                while (!finished)
                {
                    var keyInfo = readKey();
                    var bufferPosition = Math.Max(
                        0,
                        Math.Min(
                            InputBuffer.Count,
                            Console.BufferWidth * (Console.CursorTop - WaitCursorTop) +
                            (Console.CursorLeft - WaitCursorLeft) -
                            (WaitPrefix?.Length ?? 0)
                        )
                    );

                    var bufferLength = InputBuffer.Count;

                    switch (keyInfo.Key)
                    {
                        case ConsoleKey.Enter:
                            line = new string([.. InputBuffer]);
                            InputBuffer.Clear();
                            if (InputHistoryFull)
                            {
                                _inputHistory.RemoveRange(0, _inputHistory.Count - _inputHistoryLength);
                            }

                            _inputHistory.Add(line);
                            InputHistoryPosition = _inputHistory.Count;
                            writeLine();
                            MoveWaitCursor();
                            finished = true;

                            break;

                        case ConsoleKey.Backspace:
                            --bufferPosition;
                            if (-1 < bufferPosition && bufferPosition < InputBuffer.Count)
                            {
                                InputBuffer.RemoveAt(bufferPosition);
                            }

                            break;

                        case ConsoleKey.Delete:
                            if (bufferPosition < InputBuffer.Count)
                            {
                                InputBuffer.RemoveAt(bufferPosition);
                            }

                            break;

                        case ConsoleKey.UpArrow:
                            --InputHistoryPosition;
                            if (_inputHistory.Count > InputHistoryPosition)
                            {
                                InputBuffer.Clear();
                                InputBuffer.AddRange(
                                    _inputHistory[InputHistoryPosition]?.ToCharArray() ?? []
                                );
                            }

                            break;

                        case ConsoleKey.DownArrow:
                            ++InputHistoryPosition;
                            if (_inputHistory.Count > InputHistoryPosition)
                            {
                                InputBuffer.Clear();
                                InputBuffer.AddRange(
                                    _inputHistory[InputHistoryPosition]?.ToCharArray() ?? []
                                );
                            }
                            else if (_inputHistory.Count == InputHistoryPosition && InputHistoryPosition > 0)
                            {
                                InputBuffer.Clear();
                            }

                            break;

                        case ConsoleKey.LeftArrow:
                            --bufferPosition;

                            break;

                        case ConsoleKey.RightArrow:
                            ++bufferPosition;

                            break;

                        #region Default key handling

                        case ConsoleKey.Tab:
                        case ConsoleKey.Clear:
                        case ConsoleKey.Pause:
                        case ConsoleKey.Escape:
                        case ConsoleKey.Spacebar:
                        case ConsoleKey.PageUp:
                        case ConsoleKey.PageDown:
                        case ConsoleKey.End:
                        case ConsoleKey.Home:
                        case ConsoleKey.Select:
                        case ConsoleKey.Print:
                        case ConsoleKey.Execute:
                        case ConsoleKey.PrintScreen:
                        case ConsoleKey.Insert:
                        case ConsoleKey.Help:
                        case ConsoleKey.D0:
                        case ConsoleKey.D1:
                        case ConsoleKey.D2:
                        case ConsoleKey.D3:
                        case ConsoleKey.D4:
                        case ConsoleKey.D5:
                        case ConsoleKey.D6:
                        case ConsoleKey.D7:
                        case ConsoleKey.D8:
                        case ConsoleKey.D9:
                        case ConsoleKey.A:
                        case ConsoleKey.B:
                        case ConsoleKey.C:
                        case ConsoleKey.D:
                        case ConsoleKey.E:
                        case ConsoleKey.F:
                        case ConsoleKey.G:
                        case ConsoleKey.H:
                        case ConsoleKey.I:
                        case ConsoleKey.J:
                        case ConsoleKey.K:
                        case ConsoleKey.L:
                        case ConsoleKey.M:
                        case ConsoleKey.N:
                        case ConsoleKey.O:
                        case ConsoleKey.P:
                        case ConsoleKey.Q:
                        case ConsoleKey.R:
                        case ConsoleKey.S:
                        case ConsoleKey.T:
                        case ConsoleKey.U:
                        case ConsoleKey.V:
                        case ConsoleKey.W:
                        case ConsoleKey.X:
                        case ConsoleKey.Y:
                        case ConsoleKey.Z:
                        case ConsoleKey.LeftWindows:
                        case ConsoleKey.RightWindows:
                        case ConsoleKey.Applications:
                        case ConsoleKey.Sleep:
                        case ConsoleKey.NumPad0:
                        case ConsoleKey.NumPad1:
                        case ConsoleKey.NumPad2:
                        case ConsoleKey.NumPad3:
                        case ConsoleKey.NumPad4:
                        case ConsoleKey.NumPad5:
                        case ConsoleKey.NumPad6:
                        case ConsoleKey.NumPad7:
                        case ConsoleKey.NumPad8:
                        case ConsoleKey.NumPad9:
                        case ConsoleKey.Multiply:
                        case ConsoleKey.Add:
                        case ConsoleKey.Separator:
                        case ConsoleKey.Subtract:
                        case ConsoleKey.Decimal:
                        case ConsoleKey.Divide:
                        case ConsoleKey.F1:
                        case ConsoleKey.F2:
                        case ConsoleKey.F3:
                        case ConsoleKey.F4:
                        case ConsoleKey.F5:
                        case ConsoleKey.F6:
                        case ConsoleKey.F7:
                        case ConsoleKey.F8:
                        case ConsoleKey.F9:
                        case ConsoleKey.F10:
                        case ConsoleKey.F11:
                        case ConsoleKey.F12:
                        case ConsoleKey.F13:
                        case ConsoleKey.F14:
                        case ConsoleKey.F15:
                        case ConsoleKey.F16:
                        case ConsoleKey.F17:
                        case ConsoleKey.F18:
                        case ConsoleKey.F19:
                        case ConsoleKey.F20:
                        case ConsoleKey.F21:
                        case ConsoleKey.F22:
                        case ConsoleKey.F23:
                        case ConsoleKey.F24:
                        case ConsoleKey.BrowserBack:
                        case ConsoleKey.BrowserForward:
                        case ConsoleKey.BrowserRefresh:
                        case ConsoleKey.BrowserStop:
                        case ConsoleKey.BrowserSearch:
                        case ConsoleKey.BrowserFavorites:
                        case ConsoleKey.BrowserHome:
                        case ConsoleKey.VolumeMute:
                        case ConsoleKey.VolumeDown:
                        case ConsoleKey.VolumeUp:
                        case ConsoleKey.MediaNext:
                        case ConsoleKey.MediaPrevious:
                        case ConsoleKey.MediaStop:
                        case ConsoleKey.MediaPlay:
                        case ConsoleKey.LaunchMail:
                        case ConsoleKey.LaunchMediaSelect:
                        case ConsoleKey.LaunchApp1:
                        case ConsoleKey.LaunchApp2:
                        case ConsoleKey.Oem1:
                        case ConsoleKey.OemPlus:
                        case ConsoleKey.OemComma:
                        case ConsoleKey.OemMinus:
                        case ConsoleKey.OemPeriod:
                        case ConsoleKey.Oem2:
                        case ConsoleKey.Oem3:
                        case ConsoleKey.Oem4:
                        case ConsoleKey.Oem5:
                        case ConsoleKey.Oem6:
                        case ConsoleKey.Oem7:
                        case ConsoleKey.Oem8:
                        case ConsoleKey.Oem102:
                        case ConsoleKey.Process:
                        case ConsoleKey.Packet:
                        case ConsoleKey.Attention:
                        case ConsoleKey.CrSel:
                        case ConsoleKey.ExSel:
                        case ConsoleKey.EraseEndOfFile:
                        case ConsoleKey.Play:
                        case ConsoleKey.Zoom:
                        case ConsoleKey.NoName:
                        case ConsoleKey.Pa1:
                        case ConsoleKey.OemClear:

                        default:
                            InputBuffer.Add(keyInfo.KeyChar);
                            ++bufferPosition;

                            break;

                            #endregion
                    }

                    // ReSharper disable once InvertIf
                    if (!finished)
                    {
                        // TODO: Soft cursor reset and prefix rewrite
                        ResetWaitCursor(write, bufferLength);
                        WritePrefix(write, bufferPosition);
                    }
                }

                return line;
            }
        );
    }
}
