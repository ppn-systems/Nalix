using Nalix.Shared.Frames.Controls;
using Nalix.Shared.Serialization;
using System;

Handshake handshake = new()
{
    Data = [0, 0, 1, 1, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0]
};

Byte[] bytes = LiteSerializer.Serialize(handshake);

System.Console.WriteLine($"Serialized Handshake: {BitConverter.ToString(bytes)}, size={bytes.Length}");