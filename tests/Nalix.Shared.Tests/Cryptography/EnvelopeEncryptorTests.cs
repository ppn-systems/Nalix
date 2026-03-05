using Nalix.Common.Attributes;
using Nalix.Common.Enums;
using Nalix.Shared.Security;
using System;
using System.Text;
using Xunit;

namespace Nalix.Shared.Tests.Cryptography;

// Tests for EnvelopeEncryptor functionality
public class EnvelopeEncryptorTests
{
    // Mock class with SensitiveDataAttribute to validate the encryptor
    private class TestClass
    {
        [SensitiveData]
        public String SensitiveProperty { get; set; }

        [SensitiveData]
        public Int64 SensitiveField;
    }

    [Fact]
    public void Encrypt_EncryptsPropertiesAndFields()
    {
        // Arrange
        var testObject = new TestClass
        {
            SensitiveProperty = "SensitiveData",
            SensitiveField = 1234
        };

        String key = "1234567890abcdef1234567890abcdef"; // 32 bytes key
        var keyBytes = Encoding.UTF8.GetBytes(key);

        // Act
        EnvelopeEncryptor.Encrypt(testObject, keyBytes, CipherSuiteType.CHACHA20_POLY1305);

        // Assert
        Assert.NotEqual("SensitiveData", testObject.SensitiveProperty);
        Assert.NotEqual(1234, testObject.SensitiveField);
    }

    [Fact]
    public void Decrypt_DecryptsPropertiesAndFields()
    {
        // Arrange
        var testObject = new TestClass
        {
            SensitiveProperty = "SensitiveData",
            SensitiveField = 1234
        };

        String key = "1234567890abcdef1234567890abcdef";
        var keyBytes = Encoding.UTF8.GetBytes(key);

        EnvelopeEncryptor.Encrypt(testObject, keyBytes, CipherSuiteType.CHACHA20_POLY1305);

        // Act
        EnvelopeEncryptor.Decrypt(testObject, keyBytes);

        // Assert
        Assert.Equal("SensitiveData", testObject.SensitiveProperty);
        Assert.Equal(1234, testObject.SensitiveField);
    }

    [Fact]
    public void HasSensitiveData_DetectsSensitiveMembers() =>
        // Assert
        Assert.True(EnvelopeEncryptor.HasSensitiveData<TestClass>());

    [Fact]
    public void GetSensitiveDataMembers_ReturnsCorrectMemberNames()
    {
        // Act
        var memberNames = EnvelopeEncryptor.GetSensitiveDataMembers<TestClass>();

        // Assert: method returns "MemberName [Level]"; test accordingly
        Assert.Contains("SensitiveProperty [High]", memberNames);
        Assert.Contains("SensitiveField [High]", memberNames);
    }
}