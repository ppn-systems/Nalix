using System;

namespace Notio.Common.INetwork.Args;

public interface IErrorEventArgs
{
    string Message { get; }
    DateTimeOffset Timestamp { get; }
}