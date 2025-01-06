using System;

namespace Notio.Common.Network.Args;

public interface IErrorEventArgs
{
    string Message { get; }
    DateTimeOffset Timestamp { get; }
}