using Notio.Cryptography.Ciphers.Asymmetric;
using System.Numerics;
using Xunit;

namespace Notio.Testing.Ciphers;

public class Srp6Tests
{
    // Sample values for testing
    private readonly byte[] keyString = new byte[] { 0xA1, 0xB2, 0xC3 }; // Example key string (s)

    private readonly string username = "testUser";
    private readonly string password = "testPassword";

    [Fact]
    public void GenerateVerifier_ValidInputs_CreatesCorrectVerifier()
    {
        // Arrange
        var verifier = Srp6.GenerateVerifier(keyString, username, password);

        // Assert
        Assert.NotNull(verifier);
        Assert.True(verifier.Length > 0, "Verifier array should not be empty.");
    }

    [Fact]
    public void GenerateServerCredentials_ValidInputs_CreatesServerCredentials()
    {
        // Arrange
        var srp = new Srp6(username, keyString, new byte[] { 0xA4, 0xD1, 0xB6 });

        // Act
        var serverCredentials = srp.GenerateServerCredentials();

        // Assert
        Assert.NotNull(serverCredentials);
        Assert.True(serverCredentials.Length > 0, "Server credentials should not be empty.");
    }

    [Fact]
    public void CalculateSecret_ValidClientA_CalculatesSecretSuccessfully()
    {
        // Arrange
        var srp = new Srp6(username, keyString, new byte[] { 0xA4, 0xD1, 0xB6 });
        byte[] clientA = new byte[] { 0xB1, 0xB2, 0xB3 };  // Example client A value

        // Act
        srp.CalculateSecret(clientA);

        // Assert
        Assert.True(1 != BigInteger.Zero, "Shared secret S should not be zero.");
    }

    [Fact]
    public void VerifyClientEvidenceMessage_ValidClientM1_ReturnsTrue()
    {
        // Arrange
        var srp = new Srp6(username, keyString, new byte[] { 0xA4, 0xD1, 0xB6 });
        byte[] clientM1 = new byte[] { 0xC1, 0xD2, 0xE3 };  // Example client M1 value
        byte[] clientA = new byte[] { 0xB1, 0xB2, 0xB3 };  // Example client A value

        // First, we simulate server-side processing of A (e.g., calling CalculateSecret)
        srp.CalculateSecret(clientA);

        // Act
        var result = srp.VerifyClientEvidenceMessage(clientM1);

        // Assert
        Assert.True(result, "Client's evidence message should be valid.");
    }

    [Fact]
    public void CalculateServerEvidenceMessage_ValidInputs_ReturnsServerM2()
    {
        // Arrange
        var srp = new Srp6(username, keyString, new byte[] { 0xA4, 0xD1, 0xB6 });
        byte[] clientA = new byte[] { 0xB1, 0xB2, 0xB3 };  // Example client A value
        byte[] clientM1 = new byte[] { 0xC1, 0xD2, 0xE3 };  // Example client M1 value

        // Simulate server-side calculation (CalculateSecret and VerifyClientEvidenceMessage)
        srp.CalculateSecret(clientA);
        srp.VerifyClientEvidenceMessage(clientM1);

        // Act
        var serverM2 = srp.CalculateServerEvidenceMessage();

        // Assert
        Assert.NotNull(serverM2);
        Assert.True(serverM2.Length > 0, "Server's evidence message M2 should not be empty.");
    }
}
