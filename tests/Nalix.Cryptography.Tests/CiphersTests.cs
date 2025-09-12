using Nalix.Common.Enums;
using Nalix.Common.Exceptions;
using System;
using System.Text;
using Xunit;

namespace Nalix.Cryptography.Tests;

public class CiphersTests
{
    // Test data (Dữ liệu test)
    private readonly Byte[] _validKey = new Byte[32]; // Key 32 bytes

    private readonly Memory<Byte> _validData; // Dữ liệu hợp lệ

    public CiphersTests()
    {
        // Initialize test data (Khởi tạo dữ liệu test)
        Random.Shared.NextBytes(_validKey);
        _validData = Encoding.UTF8.GetBytes("Hello, this is test data!").AsMemory();
    }

    [Fact(DisplayName = "Encrypt - With valid inputs should succeed")] // Kiểm tra mã hóa với đầu vào hợp lệ
    public void Encrypt_WithValidInputs_ShouldSucceed()
    {
        // Arrange & Act
        var result = Ciphers.Encrypt(_validData, _validKey, SymmetricAlgorithmType.XTEA);

        // Assert
        Assert.True(result.Length > 0);
    }

    [Fact(DisplayName = "Decrypt - Should return original data")] // Kiểm tra giải mã trả về dữ liệu gốc
    public void Decrypt_ShouldReturnOriginalData()
    {
        // Arrange
        var encrypted = Ciphers.Encrypt(_validData, _validKey, SymmetricAlgorithmType.XTEA);

        // Act
        var decrypted = Ciphers.Decrypt(encrypted, _validKey);

        // Assert
        Assert.Equal(_validData.ToArray(), decrypted.ToArray());
    }

    [Theory(DisplayName = "Encrypt - Should work with different algorithms")] // Kiểm tra mã hóa với các thuật toán khác nhau
    [InlineData(SymmetricAlgorithmType.ChaCha20Poly1305)]
    [InlineData(SymmetricAlgorithmType.Salsa20)]
    [InlineData(SymmetricAlgorithmType.Speck)]
    [InlineData(SymmetricAlgorithmType.TwofishECB)]
    [InlineData(SymmetricAlgorithmType.TwofishCBC)]
    [InlineData(SymmetricAlgorithmType.XTEA)]
    public void Encrypt_ShouldWorkWithDifferentAlgorithms(SymmetricAlgorithmType algorithm)
    {
        // Arrange & Act
        var encrypted = Ciphers.Encrypt(_validData, _validKey, algorithm);
        var decrypted = Ciphers.Decrypt(encrypted, _validKey, algorithm);

        // Assert
        Assert.Equal(_validData.ToArray(), decrypted.ToArray());
    }

    [Fact(DisplayName = "Encrypt - Null key should throw ArgumentNullException")] // Kiểm tra ném ngoại lệ khi key null
    public void Encrypt_NullKey_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentNullException>(() =>
            Ciphers.Encrypt(_validData, null!, SymmetricAlgorithmType.XTEA));

        Assert.Equal("key", exception.ParamName);
    }

    [Fact(DisplayName = "Encrypt - Empty data should throw ArgumentException")] // Kiểm tra ném ngoại lệ khi dữ liệu trống
    public void Encrypt_EmptyData_ShouldThrowArgumentException()
    {
        // Arrange
        Memory<Byte> emptyData = Array.Empty<Byte>();

        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() =>
            Ciphers.Encrypt(emptyData, _validKey, SymmetricAlgorithmType.XTEA));

        Assert.Contains("Data cannot be empty", exception.Message);
    }

    [Fact(DisplayName = "TryEncrypt - Should return true for valid inputs")] // Kiểm tra TryEncrypt với đầu vào hợp lệ
    public void TryEncrypt_ValidInputs_ShouldReturnTrue()
    {
        // Act
        Boolean success = Ciphers.TryEncrypt(_validData, _validKey, out var encrypted, SymmetricAlgorithmType.XTEA);

        // Assert
        Assert.True(success);
        Assert.NotEqual(default, encrypted);
    }

    [Fact(DisplayName = "TryDecrypt - Should return true for valid inputs")] // Kiểm tra TryDecrypt với đầu vào hợp lệ
    public void TryDecrypt_ValidInputs_ShouldReturnTrue()
    {
        // Arrange
        var encrypted = Ciphers.Encrypt(_validData, _validKey, SymmetricAlgorithmType.XTEA);

        // Act
        Boolean success = Ciphers.TryDecrypt(encrypted, _validKey, out var decrypted, SymmetricAlgorithmType.XTEA);

        // Assert
        Assert.True(success);
        Assert.Equal(_validData.ToArray(), decrypted.ToArray());
    }

    [Fact(DisplayName = "TryEncrypt - Should return false for invalid inputs")] // Kiểm tra TryEncrypt với đầu vào không hợp lệ
    public void TryEncrypt_InvalidInputs_ShouldReturnFalse()
    {
        // Act
        Boolean success = Ciphers.TryEncrypt(_validData, null!, out var encrypted, SymmetricAlgorithmType.XTEA);

        // Assert
        Assert.False(success);
        Assert.Equal(default, encrypted);
    }

    [Fact(DisplayName = "Decrypt - Invalid algorithm should throw CryptoException")] // Kiểm tra ném ngoại lệ khi thuật toán không hợp lệ
    public void Decrypt_InvalidAlgorithm_ShouldThrowCryptoException()
    {
        // Arrange
        var encrypted = Ciphers.Encrypt(_validData, _validKey, SymmetricAlgorithmType.XTEA);

        // Act & Assert
        var exception = Assert.Throws<CryptoException>(() =>
            Ciphers.Decrypt(encrypted, _validKey, (SymmetricAlgorithmType)255));

        Assert.Contains("not supported", exception.Message);
    }
}