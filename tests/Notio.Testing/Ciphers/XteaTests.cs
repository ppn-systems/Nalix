using Notio.Cryptography.Ciphers.Symmetric;
using System;
using Xunit;

namespace Notio.Testing.Ciphers;

public class XteaTests
{
    private static readonly uint[] TestKey = [0x01234567, 0x89ABCDEF, 0xFEDCBA98, 0x76543210];

    [Fact]
    public void EncryptDecrypt_ShouldReturnOriginalData()
    {
        byte[] originalData = "ABCDEFGH"u8.ToArray();
        byte[] encryptedData = new byte[originalData.Length];
        byte[] decryptedData = new byte[originalData.Length];

        Xtea.Encrypt(originalData, TestKey, encryptedData);
        Xtea.Decrypt(encryptedData, TestKey, decryptedData);

        Assert.Equal(originalData, decryptedData);
    }

    [Fact]
    public void Encrypt_ThrowsException_WhenDataIsEmpty()
    {
        byte[] emptyData = [];
        byte[] output = new byte[8];

        Assert.Throws<ArgumentException>(() => Xtea.Encrypt(emptyData, TestKey, output));
    }

    [Fact]
    public void Encrypt_ThrowsException_WhenKeyIsInvalid()
    {
        byte[] data = new byte[8];
        uint[] invalidKey = [0x12345678, 0x9ABCDEF0]; // Key length must be 4
        byte[] output = new byte[8];

        Assert.Throws<ArgumentException>(() => Xtea.Encrypt(data, invalidKey, output));
    }

    [Fact]
    public void Decrypt_ThrowsException_WhenDataIsNotMultipleOf8()
    {
        byte[] invalidData = new byte[7];
        byte[] output = new byte[8];

        Assert.Throws<ArgumentException>(() => Xtea.Decrypt(invalidData, TestKey, output));
    }

    [Fact]
    public void EncryptDecrypt_ShouldWorkWithDifferentDataSizes()
    {
        byte[] originalData = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16];
        byte[] encryptedData = new byte[16];
        byte[] decryptedData = new byte[16];

        Xtea.Encrypt(originalData, TestKey, encryptedData);
        Xtea.Decrypt(encryptedData, TestKey, decryptedData);

        Assert.Equal(originalData, decryptedData);
    }
}
