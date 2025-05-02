using System;
using System.Buffers.Binary;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace Nalix.Cryptography.Hashing;

/// <summary>
/// Provides a managed implementation of the SHA-512 hashing algorithm.
/// </summary>
[System.Runtime.InteropServices.ComVisible(true)]
internal sealed class SHA384 : IDisposable
{
    private readonly byte[] _buffer = new byte[128];
    private readonly ulong[] _state = new ulong[8];

    private ulong _byteCountHigh;
    private ulong _byteCountLow;
    private byte[] _finalHash;
    private int _bufferLength;
    private bool _finalized;
    private bool _disposed;

    public SHA384() => Initialize();

    public static byte[] HashData(ReadOnlySpan<byte> data)
    {
        using SHA384 sha = new();
        sha.Update(data);
        return sha.FinalizeHash();
    }

    public static void HashData(ReadOnlySpan<byte> data, Span<byte> output)
    {
        if (output.Length < 48)
            throw new ArgumentException("Output must be at least 48 bytes.", nameof(output));

        using SHA384 sha = new();
        sha.Update(data);
        sha.FinalizeHash().AsSpan().CopyTo(output);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Initialize()
    {
        Buffer.BlockCopy(SHA.H384, 0, _state, 0, SHA.H384.Length * sizeof(ulong));
        _bufferLength = 0;
        _byteCountLow = 0;
        _byteCountHigh = 0;
        _finalized = false;
        _disposed = false;
        _finalHash = null;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Update(ReadOnlySpan<byte> data)
    {
        ObjectDisposedException.ThrowIf(_disposed, nameof(SHA384));
        if (_finalized) throw new InvalidOperationException("Hash already finalized.");

        ulong bits = (ulong)data.Length;
        _byteCountLow += bits;
        if (_byteCountLow < bits) _byteCountHigh++;

        if (_bufferLength > 0)
        {
            int toFill = 128 - _bufferLength;
            if (data.Length < toFill)
            {
                data.CopyTo(_buffer.AsSpan(_bufferLength));
                _bufferLength += data.Length;
                return;
            }

            data[..toFill].CopyTo(_buffer.AsSpan(_bufferLength));
            ProcessBlock(_buffer);
            data = data[toFill..];
        }

        while (data.Length >= 128)
        {
            ProcessBlock(data[..128]);
            data = data[128..];
        }

        data.CopyTo(_buffer);
        _bufferLength = data.Length;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public byte[] FinalizeHash()
    {
        ObjectDisposedException.ThrowIf(_disposed, nameof(SHA384));
        if (_finalized) return (byte[])_finalHash.Clone();

        Span<byte> padding = stackalloc byte[256];
        padding.Clear();
        padding[0] = 0x80;

        int padLength = (_bufferLength < 112) ? (112 - _bufferLength) : (240 - _bufferLength);
        Update(padding[..padLength]);

        Span<byte> lengthBlock = stackalloc byte[16];
        BinaryPrimitives.WriteUInt64BigEndian(lengthBlock[..8], _byteCountHigh << 3 | _byteCountLow >> 61);
        BinaryPrimitives.WriteUInt64BigEndian(lengthBlock[8..], _byteCountLow << 3);
        Update(lengthBlock);

        byte[] result = new byte[48]; // Only 6 state words used
        for (int i = 0; i < 6; i++)
            BinaryPrimitives.WriteUInt64BigEndian(result.AsSpan(i * 8), _state[i]);

        _finalHash = result;
        _finalized = true;
        return result;
    }

    public byte[] ComputeHash(ReadOnlySpan<byte> data)
    {
        Update(data);
        return FinalizeHash();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private unsafe void ProcessBlock(ReadOnlySpan<byte> block)
    {
        const int rounds = 80;
        Span<ulong> w = stackalloc ulong[80];

        for (int i = 0; i < 16; i++)
            w[i] = BinaryPrimitives.ReadUInt64BigEndian(block.Slice(i * 8, 8));

        for (int i = 16; i < rounds; i++)
        {
            ulong s0 = BitOperations.RotateRight(w[i - 15], 1) ^ BitOperations.RotateRight(w[i - 15], 8) ^ (w[i - 15] >> 7);
            ulong s1 = BitOperations.RotateRight(w[i - 2], 19) ^ BitOperations.RotateRight(w[i - 2], 61) ^ (w[i - 2] >> 6);
            w[i] = unchecked(w[i - 16] + s0 + w[i - 7] + s1);
        }

        ulong a = _state[0], b = _state[1], c = _state[2], d = _state[3];
        ulong e = _state[4], f = _state[5], g = _state[6], h = _state[7];

        ReadOnlySpan<ulong> K = SHA.K512;

        for (int i = 0; i < rounds; i++)
        {
            ulong S1 = BitOperations.RotateRight(e, 14) ^ BitOperations.RotateRight(e, 18) ^ BitOperations.RotateRight(e, 41);
            ulong ch = (e & f) ^ (~e & g);
            ulong temp1 = h + S1 + ch + K[i] + w[i];
            ulong S0 = BitOperations.RotateRight(a, 28) ^ BitOperations.RotateRight(a, 34) ^ BitOperations.RotateRight(a, 39);
            ulong maj = (a & b) ^ (a & c) ^ (b & c);
            ulong temp2 = S0 + maj;

            h = g;
            g = f;
            f = e;
            e = d + temp1;
            d = c;
            c = b;
            b = a;
            a = temp1 + temp2;
        }

        _state[0] += a; _state[1] += b; _state[2] += c; _state[3] += d;
        _state[4] += e; _state[5] += f; _state[6] += g; _state[7] += h;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        if (_finalHash != null) Array.Clear(_finalHash, 0, _finalHash.Length);
        Array.Clear(_state, 0, _state.Length);
    }
}
