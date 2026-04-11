using System;
using Nalix.Framework.DataFrames.Transforms;
using Nalix.Framework.Security;

public class Program {
    public static void Main() {
        Console.WriteLine($"FrameTransformer.Offset: {FrameTransformer.Offset}");
        Console.WriteLine($"EnvelopeCipher.HeaderSize: {EnvelopeCipher.HeaderSize}");
    }
}
