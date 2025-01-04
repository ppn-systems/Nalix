using Notio.Common.IMemory;
using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Notio.Packets;

[StructLayout(LayoutKind.Auto)]
[method: MethodImpl(MethodImplOptions.AggressiveInlining)]
public readonly struct Packet(
    byte type, byte flags, short command, ReadOnlyMemory<byte> payload) 
    : IEquatable<Packet>, IPoolable
{
    public byte Type { get; init; } = type;
    public byte Flags { get; init; } = flags;
    public short Command { get; init; } = command;
    public ReadOnlyMemory<byte> Payload { get; init; } = payload;

    // Value equality
    public bool Equals(Packet other) =>
        Type == other.Type &&
        Flags == other.Flags &&
        Command == other.Command &&
        Payload.Span.SequenceEqual(other.Payload.Span);

    public override bool Equals(object? obj) =>
        obj is Packet other && Equals(other);

    public override int GetHashCode()
    {
        HashCode hash = new();
        hash.Add(Type);
        hash.Add(Flags);
        hash.Add(Command);
        hash.Add(Payload.Length); // Use length as part of hash
        return hash.ToHashCode();
    }

    public void ResetForPool()
    {
        // Optionally reset fields if necessary
    }

    // Operator overloads
    public static bool operator ==(Packet left, Packet right) => left.Equals(right);
    public static bool operator !=(Packet left, Packet right) => !(left == right);

    // Implicit conversion
    public static implicit operator ReadOnlyMemory<byte>(Packet packet) => packet.Payload;
}