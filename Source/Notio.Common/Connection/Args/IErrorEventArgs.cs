using System;

namespace Notio.Common.Connection.Args;

public interface IErrorEventArgs
{
    string Message { get; }
    DateTimeOffset Timestamp { get; }
}