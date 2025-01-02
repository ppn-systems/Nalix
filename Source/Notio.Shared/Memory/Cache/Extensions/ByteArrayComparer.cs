using System;
using System.Collections.Generic;
using System.Linq;

namespace Notio.Shared.Memory.Cache.Extensions;

internal sealed class ByteArrayComparer : IEqualityComparer<byte[]>
{
    public bool Equals(byte[]? x, byte[]? y) => x == null ? y == null : y != null && x.SequenceEqual(y);

    public int GetHashCode(byte[] obj) => obj.Aggregate(17, (hash, b) => hash * 31 + b);
}