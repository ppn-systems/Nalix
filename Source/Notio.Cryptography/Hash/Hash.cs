using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Notio.Cryptography.Hash;

/// <summary>
/// An output of the default size, 32 bytes, which provides constant-time equality checking.
/// </summary>
/// <remarks>
/// This hash is returned by <see cref="Hasher.Hash(ReadOnlySpan{byte})"/>.
/// This hash struct provides structural equality.
/// </remarks>
public struct Hash : IEquatable<Hash>
{
    /// <summary>
    /// The size of this hash is 32 bytes.
    /// </summary>
    public const int Size = 32;

    /// <summary>
    /// Copies bytes to this hash. The input data must be 32 bytes.
    /// </summary>
    /// <param name="data">A 32-byte buffer.</param>
    public void CopyFromBytes(ReadOnlySpan<byte> data)
    {
        if (data.Length != 32) ThrowArgumentOutOfRange(data.Length);
        data.CopyTo(AsSpan());
    }

    /// <summary>
    /// Creates a hash from an input data that must be 32 bytes.
    /// </summary>
    /// <param name="data">A 32-byte buffer.</param>
    /// <returns>The 32-byte hash.</returns>
    [SkipLocalsInit]
    public static Hash FromBytes(ReadOnlySpan<byte> data)
    {
        if (data.Length != 32) ThrowArgumentOutOfRange(data.Length);
        var hash = new Hash();
        hash.CopyFromBytes(data);
        return hash;
    }

    public bool Equals(Hash other)
    {
        return AsSpan().SequenceCompareTo(other.AsSpan()) == 0;
    }

    public override bool Equals(object obj)
    {
        return obj is Hash other && Equals(other);
    }

    public override int GetHashCode()
    {
        var values = MemoryMarshal.Cast<byte, int>(AsSpan());
        int hashcode = 0;
        for (int i = 0; i < values.Length; i++)
        {
            hashcode = hashcode * 397 ^ values[i];
        }
        return hashcode;
    }

    public override readonly string ToString()
    {
        return string.Create(Size * 2, this, (span, hash) =>
        {
            var data = hash.AsSpan();
            for (int i = 0; i < data.Length; i++)
            {
                var b = data[i];
                span[i * 2] = (char)Hex[b >> 4 & 0xF];
                span[i * 2 + 1] = (char)Hex[b & 0xF];
            }
        });
    }

    /// <summary>
    /// Creates a span from a hash. The span returned has to follow the same lifetime than the hash referenced.
    /// </summary>
    /// <returns>The hash of the span</returns>
    [UnscopedRef]
    public Span<byte> AsSpan()
    {
        return MemoryMarshal.AsBytes(MemoryMarshal.CreateSpan(ref this, 1));
    }

    public static bool operator ==(Hash left, Hash right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(Hash left, Hash right)
    {
        return !left.Equals(right);
    }

    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ThrowArgumentOutOfRange(int size)
    {
        throw new ArgumentOutOfRangeException(nameof(size), $"Invalid size {size} of the data. Expecting 32");
    }

    private static ReadOnlySpan<byte> Hex => new("0123456789abcdef"u8.ToArray());
}