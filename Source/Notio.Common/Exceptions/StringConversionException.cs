using System;

namespace Notio.Common.Exceptions;

[Serializable]
public class StringConversionException : Exception
{
    public readonly Type Type;
    public readonly Exception Ex;

    public StringConversionException()
    {
    }

    public StringConversionException(string message) : base(message)
    {
    }

    public StringConversionException(Type type, string message) : base(message)
    {
        this.Type = type;
    }

    public StringConversionException(Type type, Exception ex)
    {
        this.Type = type;
        this.Ex = ex;
    }

    public StringConversionException(string message, Exception? innerException) : base(message, innerException)
    {
    }
}