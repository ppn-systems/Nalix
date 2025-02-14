using System;
using System.Security.Cryptography;
using Xunit;

namespace Notio.Testing.Hash;

public class SHA256Tests
{
    [Fact]
    public void HashData_EmptyInput_ReturnsCorrectHash()
    {
        var data = Array.Empty<byte>();
        var expected = System.Security.Cryptography.SHA256.HashData(data);
        var actual = SHA256.HashData(data);
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void HashData_AbcInput_ReturnsExpectedHash()
    {
        var data = System.Text.Encoding.UTF8.GetBytes("abc");
        var expected = System.Security.Cryptography.SHA256.HashData(data);
        var actual = SHA256.HashData(data);
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void HashData_MillionA_ReturnsExpectedHash()
    {
        var data = new byte[1000000];
        Array.Fill(data, (byte)'a');
        var expected = System.Security.Cryptography.SHA256.HashData(data);
        var actual = SHA256.HashData(data);
        Assert.Equal(expected, actual);
    }
}
