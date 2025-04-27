using Nalix.Shared.LZ4;
using Nalix.Shared.LZ4.Encoders;
using System;
using System.Diagnostics;

namespace Nalix.Application;

internal class Program
{
    private static void Main(string[] args)
    {
        try
        {
            // Test data - 10 KB for testing
            byte[] testData = new byte[23_02];  // 10 KB of data for testing
            int size = FastPath.GetMaxCompressedLength(testData.Length);
            // Prepare buffers for compression and decompression
            // Increase size to ensure enough space for compression and decompression
            byte[] compressedData = new byte[size];  // Increased space for compression
            Console.WriteLine($"Buffer size for compression: {size} bytes");

            // Measure compression performance
            Console.WriteLine("Starting compression test...");
            Stopwatch stopwatch = Stopwatch.StartNew();
            int compressedSize = LZ4Codec.Encode(testData, compressedData);

            stopwatch.Stop();

            Array.Resize(ref compressedData, compressedSize);

            if (compressedSize == -1)
            {
                Console.WriteLine("Compression failed.");
                return;
            }

            Console.WriteLine($"Compression time: {stopwatch.ElapsedMilliseconds} ms");
            Console.WriteLine($"Compressed size: {compressedSize} bytes");

            byte[] decompressedData = new byte[testData.Length];  // Make the decompressed buffer bigger

            // Measure decompression performance
            Console.WriteLine("Starting decompression test...");
            stopwatch.Restart();
            int decompressedSize = LZ4Codec.Decode(compressedData, decompressedData);
            stopwatch.Stop();

            if (decompressedSize == -1)
            {
                Console.WriteLine("Decompression failed.");
                return;
            }

            Console.WriteLine($"Decompression time: {stopwatch.ElapsedMilliseconds} ms");
            Console.WriteLine($"Decompressed size: {decompressedSize} bytes");

            // Validate the decompressed data matches the original
            bool isValid = ValidateDecompression(testData, decompressedData);
            Console.WriteLine($"Decompression validation: {(isValid ? "Passed" : "Failed")}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"An error occurred: {ex.Message}");
        }
    }

    // Helper function to validate if decompressed data matches original
    private static bool ValidateDecompression(byte[] original, byte[] decompressed)
    {
        if (original.Length != decompressed.Length)
            return false;

        for (int i = 0; i < original.Length; i++)
        {
            if (original[i] != decompressed[i])
                return false;
        }

        return true;
    }
}
