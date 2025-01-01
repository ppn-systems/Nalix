using System;

namespace Notio.Common.Networking.Args;

public interface IErrorEventArgs
{
    string Message { get; }
    DateTimeOffset Timestamp { get; }
}
