using Nalix.Common.Enums;
using Nalix.Shared.Security;
using System;
using Xunit;

namespace Nalix.Shared.Tests.Cryptography;

public sealed class EnvelopeEncryptorTests
{
    // NOTE: Adjust key/algorithm to match your real crypto implementation.
    // Key length must be valid for the chosen CipherSuiteType.
    private static readonly Byte[] _testKey =
    [
        0x01, 0x02, 0x03, 0x04,
        0x05, 0x06, 0x07, 0x08,
        0x09, 0x0A, 0x0B, 0x0C,
        0x0D, 0x0E, 0x0F, 0x10,
        0x11, 0x12, 0x13, 0x14,
        0x15, 0x16, 0x17, 0x18,
        0x19, 0x1A, 0x1B, 0x1C,
        0x1D, 0x1E, 0x1F, 0x20,
    ];

    private const CipherSuiteType TestCipher = CipherSuiteType.SALSA20; // Ví dụ, đổi nếu enum khác

    private static readonly Byte[] _aad = [0xAA, 0xBB, 0xCC];

    [Fact]
    public void Encrypt_Then_Decrypt_Restores_String_And_ValueType()
    {
        // Arrange
        var model = new SimpleSensitiveModel
        {
            SecretString = "top-secret",
            SecretNumber = 42,
            NonSensitive = "leave-me-as-is"
        };

        // Act
        EnvelopeEncryptor.Encrypt(model, _testKey, TestCipher, _aad);
        var encryptedString = model.SecretString;
        var encryptedNumber = model.SecretNumber;

        EnvelopeEncryptor.Decrypt(model, _testKey, _aad);

        // Assert
        Assert.Equal("top-secret", model.SecretString);
        Assert.Equal(42, model.SecretNumber);

        // Non-sensitive member should never be touched
        Assert.Equal("leave-me-as-is", model.NonSensitive);

        // Encrypted representation must differ from original for string (basic sanity)
        Assert.NotEqual("top-secret", encryptedString);
        // Value type is replaced by default(T) during encryption
        Assert.Equal(default, encryptedNumber);
    }

    [Fact]
    public void Encrypt_Then_Decrypt_Nested_List_Array_And_Object()
    {
        // Arrange
        var model = new ComplexSensitiveModel
        {
            RootSecret = "root",
            Children =
            [
                new() { ChildSecret = "child-1" },
                new() { ChildSecret = "child-2" },
            ],
            ChildArray = new[]
            {
                new NestedChildModel { ChildSecret = "array-1" },
                new NestedChildModel { ChildSecret = "array-2" }
            },
            SingleChild = new NestedChildModel
            {
                ChildSecret = "single"
            }
        };

        // Act
        EnvelopeEncryptor.Encrypt(model, _testKey, TestCipher, _aad);

        // Capture encrypted state
        String encryptedRoot = model.RootSecret;
        String encryptedChild0 = model.Children![0].ChildSecret;
        String encryptedArray0 = model.ChildArray![0].ChildSecret;
        String encryptedSingle = model.SingleChild!.ChildSecret;

        EnvelopeEncryptor.Decrypt(model, _testKey, _aad);

        // Assert – root
        Assert.Equal("root", model.RootSecret);
        Assert.NotEqual("root", encryptedRoot);

        // Assert – list
        Assert.NotNull(model.Children);
        Assert.Equal(2, model.Children!.Count);
        Assert.Equal("child-1", model.Children![0].ChildSecret);
        Assert.Equal("child-2", model.Children![1].ChildSecret);
        Assert.NotEqual("child-1", encryptedChild0);

        // Assert – array
        Assert.NotNull(model.ChildArray);
        Assert.Equal(2, model.ChildArray!.Length);
        Assert.Equal("array-1", model.ChildArray![0].ChildSecret);
        Assert.Equal("array-2", model.ChildArray![1].ChildSecret);
        Assert.NotEqual("array-1", encryptedArray0);

        // Assert – single nested object
        Assert.NotNull(model.SingleChild);
        Assert.Equal("single", model.SingleChild!.ChildSecret);
        Assert.NotEqual("single", encryptedSingle);
    }

    [Fact]
    public void HasSensitiveData_Returns_True_When_Annotated_Members_Exist()
    {
        // Act
        Boolean simpleHasSensitive = EnvelopeEncryptor.HasSensitiveData<SimpleSensitiveModel>();
        Boolean complexHasSensitive = EnvelopeEncryptor.HasSensitiveData<ComplexSensitiveModel>();

        // Assert
        Assert.True(simpleHasSensitive);
        Assert.True(complexHasSensitive);
    }

    [Fact]
    public void GetSensitiveDataMembers_Returns_MemberNames_And_Levels()
    {
        // Act
        String[] members = EnvelopeEncryptor.GetSensitiveDataMembers<SimpleSensitiveModel>();

        // Assert
        Assert.NotEmpty(members);
        // Expected members: SecretString, SecretNumber, NonSensitive (all have the attribute)
        Assert.Contains(members, m => m.StartsWith("SecretString [", StringComparison.Ordinal));
        Assert.Contains(members, m => m.StartsWith("SecretNumber [", StringComparison.Ordinal));
        Assert.Contains(members, m => m.StartsWith("NonSensitive [", StringComparison.Ordinal));
    }

    [Fact]
    public void Encrypt_Does_Nothing_When_No_SensitiveData_Attribute()
    {
        // Arrange
        var obj = new NoSensitiveModel
        {
            Name = "test",
            Age = 10
        };

        // Act
        EnvelopeEncryptor.Encrypt(obj, _testKey, TestCipher, _aad);
        EnvelopeEncryptor.Decrypt(obj, _testKey, _aad);

        // Assert – object is untouched
        Assert.Equal("test", obj.Name);
        Assert.Equal(10, obj.Age);
    }

    private sealed class NoSensitiveModel
    {
        public String Name { get; set; }

        public Int32 Age { get; set; }
    }
}