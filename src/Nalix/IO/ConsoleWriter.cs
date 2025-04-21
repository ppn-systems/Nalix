using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace Nalix.IO;

/// <summary>
/// Represents a custom console writer that integrates with a <see cref="ConsoleContext"/> 
/// for managing output with cursors and prefixes.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="ConsoleWriter"/> class.
/// </remarks>
/// <param name="context">The console context used for cursor and prefix management.</param>
/// <param name="textWriter">The underlying text writer to delegate operations to.</param>
public partial class ConsoleWriter(ConsoleContext context, TextWriter textWriter) : TextWriter
{
    #region Properties

    /// <summary>
    /// Gets the console context used for cursor and prefix management.
    /// </summary>
    protected ConsoleContext Context { get; } = context;

    /// <summary>
    /// Gets the underlying text writer that handles the actual writing operations.
    /// </summary>
    internal TextWriter TextWriter { get; } = textWriter;

    /// <summary>
    /// Gets or sets a value indicating whether the next character write operation should be skipped.
    /// </summary>
    internal bool SkipNextWriteChar { get; set; }

    /// <inheritdoc />
    public override Encoding Encoding => TextWriter.Encoding;

    /// <inheritdoc />
    public override IFormatProvider FormatProvider => TextWriter.FormatProvider;

    /// <inheritdoc />
    public override string NewLine
    {
        get => TextWriter.NewLine;
        set => TextWriter.NewLine = value;
    }

    #endregion

    #region Passthrough

    /// <inheritdoc />
    public override string ToString() => TextWriter.ToString();

    /// <inheritdoc />
    public override bool Equals(object obj) => TextWriter.Equals(obj);

    /// <inheritdoc />
    public override int GetHashCode() => TextWriter.GetHashCode();

    /// <inheritdoc />
    [Obsolete("This method is obsolete and will be removed in a future version.")]
    public override object InitializeLifetimeService()
        => TextWriter.InitializeLifetimeService();

    /// <inheritdoc />
    public override void Close() => TextWriter.Close();

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            TextWriter.Dispose();
        }
    }

    #region Flush

    /// <inheritdoc />
    public override void Flush() => TextWriter.Flush();

    /// <inheritdoc />
    public override Task FlushAsync() => TextWriter.FlushAsync();

    #endregion

    #endregion

    #region Write

    /// <summary>
    /// Writes a line terminator to the text stream.
    /// </summary>
    public override void WriteLine()
    {
        Context.ResetWaitCursor(TextWriter.Write);
        TextWriter.WriteLine();
        Context.WritePrefix(TextWriter.Write);
    }

    #endregion

    #region Write Buffer/Format

    /// <inheritdoc />
    public override void Write(char[] buffer)
    {
        Context.ResetWaitCursor(TextWriter.Write);
        TextWriter.Write(buffer);
        Context.WritePrefix(TextWriter.Write);
    }

    /// <inheritdoc />
    public override void Write(char[] buffer, int index, int count)
    {
        Context.ResetWaitCursor(TextWriter.Write);
        TextWriter.Write(buffer, index, count);
        Context.WritePrefix(TextWriter.Write);
    }

    /// <inheritdoc />
    public override void Write(string format, object arg0)
    {
        Context.ResetWaitCursor(TextWriter.Write);
        TextWriter.Write(format, arg0);
        Context.WritePrefix(TextWriter.Write);
    }

    /// <inheritdoc />
    public override void Write(string format, object arg0, object arg1)
    {
        Context.ResetWaitCursor(TextWriter.Write);
        TextWriter.Write(format, arg0, arg1);
        Context.WritePrefix(TextWriter.Write);
    }

    /// <inheritdoc />
    public override void Write(string format, object arg0, object arg1, object arg2)
    {
        Context.ResetWaitCursor(TextWriter.Write);
        TextWriter.Write(format, arg0, arg1, arg2);
        Context.WritePrefix(TextWriter.Write);
    }

    /// <inheritdoc />
    public override void Write(string format, params object[] arg)
    {
        Context.ResetWaitCursor(TextWriter.Write);
        TextWriter.Write(format, arg);
        Context.WritePrefix(TextWriter.Write);
    }

    #endregion

    #region Write(value)

    /// <inheritdoc />
    public override void Write(char value)
    {
        var skip = SkipNextWriteChar;
        if (skip)
        {
            SkipNextWriteChar = false;
        }
        else
        {
            Context.ResetWaitCursor(TextWriter.Write);
            TextWriter.Write(value);
            Context.WritePrefix(TextWriter.Write);
        }
    }

    /// <inheritdoc />
    public override void Write(bool value)
    {
        Context.ResetWaitCursor(TextWriter.Write);
        TextWriter.Write(value);
        Context.WritePrefix(TextWriter.Write);
    }

    /// <inheritdoc />
    public override void Write(int value)
    {
        Context.ResetWaitCursor(TextWriter.Write);
        TextWriter.Write(value);
        Context.WritePrefix(TextWriter.Write);
    }

    /// <inheritdoc />
    public override void Write(uint value)
    {
        Context.ResetWaitCursor(TextWriter.Write);
        TextWriter.Write(value);
        Context.WritePrefix(TextWriter.Write);
    }

    /// <inheritdoc />
    public override void Write(long value)
    {
        Context.ResetWaitCursor(TextWriter.Write);
        TextWriter.Write(value);
        Context.WritePrefix(TextWriter.Write);
    }

    /// <inheritdoc />
    public override void Write(ulong value)
    {
        Context.ResetWaitCursor(TextWriter.Write);
        TextWriter.Write(value);
        Context.WritePrefix(TextWriter.Write);
    }

    /// <inheritdoc />
    public override void Write(float value)
    {
        Context.ResetWaitCursor(TextWriter.Write);
        TextWriter.Write(value);
        Context.WritePrefix(TextWriter.Write);
    }

    /// <inheritdoc />
    public override void Write(double value)
    {
        Context.ResetWaitCursor(TextWriter.Write);
        TextWriter.Write(value);
        Context.WritePrefix(TextWriter.Write);
    }

    /// <inheritdoc />
    public override void Write(decimal value)
    {
        Context.ResetWaitCursor(TextWriter.Write);
        TextWriter.Write(value);
        Context.WritePrefix(TextWriter.Write);
    }

    /// <inheritdoc />
    public override void Write(string value)
    {
        Context.ResetWaitCursor(TextWriter.Write);
        TextWriter.Write(value);
        Context.WritePrefix(TextWriter.Write);
    }

    /// <inheritdoc />
    public override void Write(object value)
    {
        Context.ResetWaitCursor(TextWriter.Write);
        TextWriter.Write(value);
        Context.WritePrefix(TextWriter.Write);
    }

    #endregion

    #region WriteAsync

    /// <inheritdoc />
    public override async Task WriteAsync(char value)
    {
        await Context.ResetWaitCursorAsync(TextWriter.WriteAsync);
        var task = TextWriter.WriteAsync(value);
        if (task != null)
        {
            await task;
        }

        await Context.WritePrefixAsync(TextWriter.WriteAsync);
    }

    /// <inheritdoc />
    public override async Task WriteAsync(string value)
    {
        await Context.ResetWaitCursorAsync(TextWriter.WriteAsync);
        var task = TextWriter.WriteAsync(value);
        if (task != null)
        {
            await task;
        }

        await Context.WritePrefixAsync(TextWriter.WriteAsync);
    }

    /// <inheritdoc />
    public override async Task WriteAsync(char[] buffer, int index, int count)
    {
        await Context.ResetWaitCursorAsync(TextWriter.WriteAsync);
        var task = TextWriter.WriteAsync(buffer, index, count);
        if (task != null)
        {
            await task;
        }

        await Context.WritePrefixAsync(TextWriter.WriteAsync);
    }

    #endregion

    #region WriteLine Buffer/Format

    /// <inheritdoc />
    public override void WriteLine(char[] buffer)
    {
        Context.ResetWaitCursor(TextWriter.Write);
        TextWriter.WriteLine(buffer);
        Context.WritePrefix(TextWriter.Write);
    }

    /// <inheritdoc />
    public override void WriteLine(char[] buffer, int index, int count)
    {
        Context.ResetWaitCursor(TextWriter.Write);
        TextWriter.WriteLine(buffer, index, count);
        Context.WritePrefix(TextWriter.Write);
    }

    /// <inheritdoc />
    public override void WriteLine(string format, object arg0)
    {
        Context.ResetWaitCursor(TextWriter.Write);
        TextWriter.WriteLine(format, arg0);
        Context.WritePrefix(TextWriter.Write);
    }

    /// <inheritdoc />
    public override void WriteLine(string format, object arg0, object arg1)
    {
        Context.ResetWaitCursor(TextWriter.Write);
        TextWriter.WriteLine(format, arg0, arg1);
        Context.WritePrefix(TextWriter.Write);
    }

    /// <inheritdoc />
    public override void WriteLine(string format, object arg0, object arg1, object arg2)
    {
        Context.ResetWaitCursor(TextWriter.Write);
        TextWriter.WriteLine(format, arg0, arg1, arg2);
        Context.WritePrefix(TextWriter.Write);
    }

    /// <inheritdoc />
    public override void WriteLine(string format, params object[] arg)
    {
        Context.ResetWaitCursor(TextWriter.Write);
        TextWriter.WriteLine(format, arg);
        Context.WritePrefix(TextWriter.Write);
    }

    #endregion

    #region WriteLine(value)

    /// <inheritdoc />
    public override void WriteLine(char value)
    {
        Context.ResetWaitCursor(TextWriter.Write);
        TextWriter.WriteLine(value);
        Context.WritePrefix(TextWriter.Write);
    }

    /// <inheritdoc />
    public override void WriteLine(bool value)
    {
        Context.ResetWaitCursor(TextWriter.Write);
        TextWriter.WriteLine(value);
        Context.WritePrefix(TextWriter.Write);
    }

    /// <inheritdoc />
    public override void WriteLine(int value)
    {
        Context.ResetWaitCursor(TextWriter.Write);
        TextWriter.WriteLine(value);
        Context.WritePrefix(TextWriter.Write);
    }

    /// <inheritdoc />
    public override void WriteLine(uint value)
    {
        Context.ResetWaitCursor(TextWriter.Write);
        TextWriter.WriteLine(value);
        Context.WritePrefix(TextWriter.Write);
    }

    /// <inheritdoc />
    public override void WriteLine(long value)
    {
        Context.ResetWaitCursor(TextWriter.Write);
        TextWriter.WriteLine(value);
        Context.WritePrefix(TextWriter.Write);
    }

    /// <inheritdoc />
    public override void WriteLine(ulong value)
    {
        Context.ResetWaitCursor(TextWriter.Write);
        TextWriter.WriteLine(value);
        Context.WritePrefix(TextWriter.Write);
    }

    /// <inheritdoc />
    public override void WriteLine(float value)
    {
        Context.ResetWaitCursor(TextWriter.Write);
        TextWriter.WriteLine(value);
        Context.WritePrefix(TextWriter.Write);
    }

    /// <inheritdoc />
    public override void WriteLine(double value)
    {
        Context.ResetWaitCursor(TextWriter.Write);
        TextWriter.WriteLine(value);
        Context.WritePrefix(TextWriter.Write);
    }

    /// <inheritdoc />
    public override void WriteLine(decimal value)
    {
        Context.ResetWaitCursor(TextWriter.Write);
        TextWriter.WriteLine(value);
        Context.WritePrefix(TextWriter.Write);
    }

    /// <inheritdoc />
    public override void WriteLine(string value)
    {
        Context.ResetWaitCursor(TextWriter.Write);
        TextWriter.WriteLine(value);
        Context.WritePrefix(TextWriter.Write);
    }

    /// <inheritdoc />
    public override void WriteLine(object value)
    {
        Context.ResetWaitCursor(TextWriter.Write);
        TextWriter.WriteLine(value);
        Context.WritePrefix(TextWriter.Write);
    }

    #endregion

    #region WriteLineAsync

    /// <inheritdoc />
    public override async Task WriteLineAsync(char value)
    {
        await Context.ResetWaitCursorAsync(TextWriter.WriteAsync);
        var task = TextWriter.WriteLineAsync(value);
        if (task != null)
        {
            await task;
        }

        await Context.WritePrefixAsync(TextWriter.WriteAsync);
    }

    /// <inheritdoc />
    public override async Task WriteLineAsync(string value)
    {
        await Context.ResetWaitCursorAsync(TextWriter.WriteAsync);
        var task = TextWriter.WriteLineAsync(value);
        if (task != null)
        {
            await task;
        }

        await Context.WritePrefixAsync(TextWriter.WriteAsync);
    }

    /// <inheritdoc />
    public override async Task WriteLineAsync(char[] buffer, int index, int count)
    {
        await Context.ResetWaitCursorAsync(TextWriter.WriteAsync);
        var task = TextWriter.WriteLineAsync(buffer, index, count);
        if (task != null)
        {
            await task;
        }

        await Context.WritePrefixAsync(TextWriter.WriteAsync);
    }

    /// <inheritdoc />
    public override async Task WriteLineAsync()
    {
        await Context.ResetWaitCursorAsync(TextWriter.WriteAsync);
        var task = TextWriter.WriteLineAsync();
        if (task != null)
        {
            await task;
        }

        await Context.WritePrefixAsync(TextWriter.WriteAsync);
    }

    #endregion
}
