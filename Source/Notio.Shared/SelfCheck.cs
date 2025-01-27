using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System;
using Notio.Common.Exceptions;

namespace Notio.Shared;

public static class SelfCheck
{
    public static InternalErrorException Failure(string message,
        [CallerFilePath] string filePath = "",
        [CallerLineNumber] int lineNumber = 0)
        => new(BuildMessage(message, filePath, lineNumber));

    private static string BuildMessage(string message, string filePath, int lineNumber)
    {
        StackFrame[] frames = new StackTrace().GetFrames();
        if (frames == null)
            return message;

        try
        {
            filePath = Path.GetFileName(filePath);
        }
        catch (ArgumentException)
        {
        }

        StackFrame? stackFrame = frames.FirstOrDefault((StackFrame f) => f.GetMethod()?.ReflectedType != typeof(SelfCheck));
        StringBuilder stringBuilder = new StringBuilder().Append('[')
                                                         .Append(stackFrame?.GetType().Assembly.GetName().Name ?? "<unknown>");

        if (!string.IsNullOrEmpty(filePath))
        {
            stringBuilder.Append(": ").Append(filePath);
            if (lineNumber > 0)
                stringBuilder.Append('(').Append(lineNumber).Append(')');
        }

        return stringBuilder.Append("] ").Append(message).ToString();
    }
}