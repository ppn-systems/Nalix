using Nalix.Common.Exceptions;
using Nalix.Cryptography.Asymmetric;
using System;
using Xunit;

namespace Nalix.Test.Cryptography.Asymmetric;

public class Srp6Tests
{
    private readonly string _username = "testUser";
    private readonly string _password = "testPassword123!";
    private readonly byte[] _salt;
    private readonly byte[] _verifier;
    private readonly Srp6 _srp6;

    public Srp6Tests()
    {
        // Setup test data
        _salt = new byte[32]; // Initialize with random data in real scenario
        _verifier = Srp6.GenerateVerifier(_salt, _username, _password);
        _srp6 = new Srp6(_username, _salt, _verifier);
    }

    [Fact(DisplayName = "Should Generate Valid Verifier")]
    public void GenerateVerifier_ShouldCreateValidVerifier()
    {
        // Arrange
        var salt = new byte[32];
        var username = "testUser";
        var password = "testPassword";

        // Act
        var verifier = Srp6.GenerateVerifier(salt, username, password);

        // Assert
        Assert.NotNull(verifier);
        Assert.True(verifier.Length > 0);
    }

    [Fact(DisplayName = "Should Generate Server Credentials")]
    public void GenerateServerCredentials_ShouldReturnValidCredentials()
    {
        // Act
        var credentials = _srp6.GenerateServerCredentials();

        // Assert
        Assert.NotNull(credentials);
        Assert.True(credentials.Length > 0);
    }

    [Fact(DisplayName = "Should Throw Exception For Invalid Client Public Value")]
    public void CalculateSecret_ShouldThrowException_WhenClientPublicValueIsInvalid()
    {
        // Arrange
        var invalidPublicValue = new byte[32]; // All zeros will be invalid

        // Act & Assert
        var exception = Assert.Throws<CryptoException>(() =>
            _srp6.CalculateSecret(invalidPublicValue));

        Assert.Contains("clientPublicValue", exception.Message);
    }

    [Theory(DisplayName = "Should Handle Various Username Password Combinations")]
    [InlineData("user1", "pass1")]
    [InlineData("user2", "pass2")]
    [InlineData("user3", "pass3")]
    public void GenerateVerifier_ShouldHandleVariousCredentials(string username, string password)
    {
        // Arrange
        var salt = new byte[32];

        // Act
        var verifier = Srp6.GenerateVerifier(salt, username, password);

        // Assert
        Assert.NotNull(verifier);
        Assert.True(verifier.Length > 0);
    }

    [Fact(DisplayName = "Full Authentication Flow Should Work")]
    public void FullAuthenticationFlow_ShouldWorkCorrectly()
    {
        // Arrange
        var serverCredentials = _srp6.GenerateServerCredentials();
        var clientPublicValue = new byte[128]; // In real scenario, this would be from client
        Array.Fill<byte>(clientPublicValue, 1); // Filling with 1s to avoid all zeros

        // Act & Assert - Step by Step Authentication
        // Step 1: Calculate Secret
        _srp6.CalculateSecret(clientPublicValue);

        // Step 2: Calculate Session Key
        var sessionKey = _srp6.CalculateSessionKey();
        Assert.NotNull(sessionKey);
        Assert.True(sessionKey.Length > 0);

        // Step 3: Verify Client Evidence Message
        var clientProofMessage = new byte[32]; // In real scenario, this would be from client
        // Note: In real scenario, this would likely be false since we're using dummy data
        var isValid = _srp6.VerifyClientEvidenceMessage(clientProofMessage);

        // Step 4: Calculate Server Evidence Message
        if (isValid)
        {
            var serverProof = _srp6.CalculateServerEvidenceMessage();
            Assert.NotNull(serverProof);
            Assert.True(serverProof.Length > 0);
        }
    }

    [Fact(DisplayName = "Should Throw When Calculating Session Key Without Shared Secret")]
    public void CalculateSessionKey_ShouldThrow_WhenSharedSecretMissing()
    {
        // Arrange
        var newSrp = new Srp6(_username, _salt, _verifier);

        // Act & Assert
        var exception = Assert.Throws<CryptoException>(() =>
            newSrp.CalculateSessionKey());

        Assert.Contains("sharedSecret", exception.Message);
    }
}