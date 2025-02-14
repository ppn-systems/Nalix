using Notio.Cryptography.Ciphers.Symmetric;
using System;
using System.Linq;
using Xunit;

namespace Notio.Testing.Ciphers;

public class Arc4Tests
{
    [Fact]
    public void EncryptDecrypt_ShouldReturnOriginalData()
    {
        byte[] key = { 1, 2, 3, 4, 5 };
        byte[] data = { 10, 20, 30, 40, 50 };
        byte[] originalData = data.ToArray();

        var cipher = new Arc4(key);
        cipher.Process(data); // Encrypt

        Assert.NotEqual(originalData, data); // Ensure data is changed

        cipher = new Arc4(key);
        cipher.Process(data); // Decrypt

        Assert.Equal(originalData, data); // Ensure it matches original
    }

    [Fact]
    public void EmptyData_ShouldRemainUnchanged()
    {
        byte[] key = { 1, 2, 3, 4, 5 };
        byte[] data = Array.Empty<byte>();

        var cipher = new Arc4(key);
        cipher.Process(data);

        Assert.Empty(data);
    }

    [Fact]
    public void InvalidKey_ShouldThrowException()
    {
        Assert.Throws<ArgumentException>(() => new Arc4(new byte[4])); // Too short
        Assert.Throws<ArgumentException>(() => new Arc4(new byte[257])); // Too long
        Assert.Throws<ArgumentException>(() => new Arc4(null)); // Null key
    }

    [Fact]
    public void IdenticalKeys_ShouldProduceSameOutput()
    {
        byte[] key = { 1, 2, 3, 4, 5 };
        byte[] data1 = { 10, 20, 30, 40, 50 };
        byte[] data2 = data1.ToArray();

        var cipher1 = new Arc4(key);
        var cipher2 = new Arc4(key);
        cipher1.Process(data1);
        cipher2.Process(data2);

        Assert.Equal(data1, data2);
    }

    [Fact]
    public void DifferentKeys_ShouldProduceDifferentOutput()
    {
        byte[] key1 = { 1, 2, 3, 4, 5 };
        byte[] key2 = { 5, 4, 3, 2, 1 };
        byte[] data1 = { 10, 20, 30, 40, 50 };
        byte[] data2 = data1.ToArray();

        var cipher1 = new Arc4(key1);
        var cipher2 = new Arc4(key2);
        cipher1.Process(data1);
        cipher2.Process(data2);

        Assert.NotEqual(data1, data2);
    }
}
