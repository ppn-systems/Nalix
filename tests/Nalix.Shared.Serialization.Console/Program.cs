// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.
using System;
using Nalix.Common.Networking.Protocols;
using Nalix.Common.Security;
using Nalix.Framework.DataFrames;
using Nalix.Framework.DataFrames.SignalFrames;
using Nalix.Framework.Memory.Buffers;

internal static class Program
{
    private static void Main(string[] args)
    {
        // Tạo gói handshake có payload 32 bytes
        byte[] data = new byte[66000];
        for (int i = 0; i < data.Length; i++)
        {
            data[i] = (byte)(i * 3);
        }

        Handshake handshake = new(
            opCode: 999,
            data: data,
            transport: ProtocolType.TCP);

        // Serialize handshake ra buffer, LƯU Ý: Không try/catch!
        int packetLength = handshake.Length;
        BufferLease src = BufferLease.Rent(packetLength);
        int written = handshake.Serialize(src.SpanFull);
        src.CommitLength(written);

        Console.WriteLine("==== RAW PACKET ====");
        Console.WriteLine($"Handshake: {handshake}");
        Console.WriteLine($"Packet Length: {packetLength}, Written: {written}");

        // ==== Encrypt payload ====
        byte[] key = new byte[32];
        for (int i = 0; i < key.Length; i++)
        {
            key[i] = (byte)(i + 1);
        }

        Console.WriteLine("==== ENCRYPTED PACKET ====");
        int lengthForEncrypt = FrameTransformer.Offset + FrameTransformer.GetMaxCiphertextSize(CipherSuiteType.Chacha20, src.Length - FrameTransformer.Offset);
        BufferLease encLease = BufferLease.Rent(lengthForEncrypt);
        Console.WriteLine($"1.{encLease.SpanFull.Length}");
        Console.WriteLine($"1.{encLease.Length}");

        bool encResult = FrameTransformer.Encrypt(src, encLease, key, CipherSuiteType.Chacha20);

        Console.WriteLine($"2.{lengthForEncrypt}");
        Console.WriteLine($"2.{encLease.Length}");
        Console.WriteLine($"{encLease.SpanFull.Length}");
        Console.WriteLine($"Encrypt Result: {encResult}");

        // ==== Decrypt payload back ====
        Console.WriteLine("==== DECRYPTED PACKET ====");
        BufferLease decLease = BufferLease.Rent(encLease.Length);
        Console.WriteLine($"1.{decLease.SpanFull.Length}");
        Console.WriteLine($"1.{decLease.Length}");

        bool decResult = FrameTransformer.Decrypt(encLease, decLease, key);


        Console.WriteLine($"2.{lengthForEncrypt}");
        Console.WriteLine($"2.{encLease.Length}");
        Console.WriteLine($"{encLease.SpanFull.Length}");
        Console.WriteLine($"Decrypt Result: {decResult}");

        // ==== Compress payload ====
        BufferLease compressLease = BufferLease.Rent(FrameTransformer.Offset + FrameTransformer.GetMaxCompressedSize(src.Length - FrameTransformer.Offset));
        bool compressResult = FrameTransformer.Compress(src, compressLease);

        Console.WriteLine("==== COMPRESSED PACKET ====");
        Console.WriteLine($"Compress Result: {compressResult}, Written: {compressLease.Length}");

        // ==== Decompress payload back ====
        int le = FrameTransformer.GetDecompressedLength(compressLease.Span[FrameTransformer.Offset..]);
        Console.WriteLine($"Decompressed Length: {le}");
        BufferLease decompressLease = BufferLease.Rent(le);
        Console.WriteLine($"1.{decompressLease.SpanFull.Length}");
        bool decompressResult = FrameTransformer.Decompress(compressLease, decompressLease);

        Console.WriteLine("==== DECOMPRESSED PACKET ====");
        Console.WriteLine($"Decompress Result: {decompressResult}, Written: {decompressLease.Length}");
    }
}
