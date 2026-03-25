// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.
using Nalix.Common.Networking.Protocols;
using Nalix.Common.Security;
using Nalix.Shared.Frames;
using Nalix.Shared.Frames.Controls;
using Nalix.Shared.Memory.Buffers;
using System;

internal static class Program
{
    private static void Main(String[] args)
    {
        // Tạo gói handshake có payload 32 bytes
        var data = new Byte[66000];
        for (Int32 i = 0; i < data.Length; i++)
        {
            data[i] = (Byte)(i * 3);
        }

        var handshake = new Handshake(
            opCode: 999,
            data: data,
            transport: ProtocolType.TCP);

        // Serialize handshake ra buffer, LƯU Ý: Không try/catch!
        Int32 packetLength = handshake.Length;
        var src = BufferLease.Rent(packetLength);
        Int32 written = handshake.Serialize(src.SpanFull);
        src.CommitLength(written);

        Console.WriteLine("==== RAW PACKET ====");
        Console.WriteLine($"Handshake: {handshake}");
        Console.WriteLine($"Packet Length: {packetLength}, Written: {written}");

        // ==== Encrypt payload ====
        var key = new Byte[32];
        for (Int32 i = 0; i < key.Length; i++)
        {
            key[i] = (Byte)(i + 1);
        }

        Console.WriteLine("==== ENCRYPTED PACKET ====");
        Int32 lengthForEncrypt = FrameTransformer.Offset + FrameTransformer.GetMaxCiphertextSize(CipherSuiteType.CHACHA20, src.Length - FrameTransformer.Offset);
        var encLease = BufferLease.Rent(lengthForEncrypt);
        Console.WriteLine($"1.{encLease.SpanFull.Length}");
        Console.WriteLine($"1.{encLease.Length}");

        Boolean encResult = FrameTransformer.Encrypt(src, encLease, key, CipherSuiteType.CHACHA20);

        Console.WriteLine($"2.{lengthForEncrypt}");
        Console.WriteLine($"2.{encLease.Length}");
        Console.WriteLine($"{encLease.SpanFull.Length}");
        Console.WriteLine($"Encrypt Result: {encResult}");

        // ==== Decrypt payload back ====
        Console.WriteLine("==== DECRYPTED PACKET ====");
        var decLease = BufferLease.Rent(encLease.Length);
        Console.WriteLine($"1.{decLease.SpanFull.Length}");
        Console.WriteLine($"1.{decLease.Length}");

        Boolean decResult = FrameTransformer.Decrypt(encLease, decLease, key);


        Console.WriteLine($"2.{lengthForEncrypt}");
        Console.WriteLine($"2.{encLease.Length}");
        Console.WriteLine($"{encLease.SpanFull.Length}");
        Console.WriteLine($"Decrypt Result: {decResult}");

        // ==== Compress payload ====
        var compressLease = BufferLease.Rent(FrameTransformer.Offset + FrameTransformer.GetMaxCompressedSize(src.Length - FrameTransformer.Offset));
        Boolean compressResult = FrameTransformer.Compress(src, compressLease);

        Console.WriteLine("==== COMPRESSED PACKET ====");
        Console.WriteLine($"Compress Result: {compressResult}, Written: {compressLease.Length}");

        // ==== Decompress payload back ====
        Int32 le = FrameTransformer.GetDecompressedLength(compressLease.Span[FrameTransformer.Offset..]);
        System.Console.WriteLine($"Decompressed Length: {le}");
        var decompressLease = BufferLease.Rent(le);
        System.Console.WriteLine($"1.{decompressLease.SpanFull.Length}");
        Boolean decompressResult = FrameTransformer.Decompress(compressLease, decompressLease);

        Console.WriteLine("==== DECOMPRESSED PACKET ====");
        Console.WriteLine($"Decompress Result: {decompressResult}, Written: {decompressLease.Length}");
    }
}