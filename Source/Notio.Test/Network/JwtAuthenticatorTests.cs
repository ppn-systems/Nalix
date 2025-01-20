using System;
using System.Collections.Generic;
using Notio.Network.Firewall;
using Notio.Common.Exceptions;

namespace Notio.Test.Network;

public sealed class JwtAuthenticatorTests
{
    private const string SecretKey = "supersecretkey123";
    private const string Issuer = "testIssuer";
    private const string Audience = "testAudience";

    public static void Main()
    {
        ConstructorThrowsExceptionWhenSecretKeyIsNullOrEmpty();
        Constructor_ThrowsException_WhenIssuerIsNullOrEmpty();
        ConstructorThrowsExceptionWhenAudienceIsNullOrEmpty();
        GenerateTokenShouldReturnValidToken();
        ValidateTokenShouldReturnTrueForValidToken();
        ValidateTokenShouldReturnFalseForInvalidSignature();
        ValidateTokenShouldReturnFalseForExpiredToken();
        ValidateTokenShouldReturnFalseForMalformedToken();

        Console.WriteLine("All tests completed.");
    }

    public static void ConstructorThrowsExceptionWhenSecretKeyIsNullOrEmpty()
    {
        try
        {
            _ = new JwtAuthenticator(null!, Issuer, Audience);
            Console.WriteLine("Test failed: No exception for null secret key.");
        }
        catch (FirewallException)
        {
            Console.WriteLine("Test passed: Exception for null secret key.");
        }

        try
        {
            _ = new JwtAuthenticator("", Issuer, Audience);
            Console.WriteLine("Test failed: No exception for empty secret key.");
        }
        catch (FirewallException)
        {
            Console.WriteLine("Test passed: Exception for empty secret key.");
        }
    }

    public static void Constructor_ThrowsException_WhenIssuerIsNullOrEmpty()
    {
        try
        {
            _ = new JwtAuthenticator(SecretKey, null!, Audience);
            Console.WriteLine("Test failed: No exception for null issuer.");
        }
        catch (FirewallException)
        {
            Console.WriteLine("Test passed: Exception for null issuer.");
        }

        try
        {
            _ = new JwtAuthenticator(SecretKey, "", Audience);
            Console.WriteLine("Test failed: No exception for empty issuer.");
        }
        catch (FirewallException)
        {
            Console.WriteLine("Test passed: Exception for empty issuer.");
        }
    }

    public static void ConstructorThrowsExceptionWhenAudienceIsNullOrEmpty()
    {
        try
        {
            _ = new JwtAuthenticator(SecretKey, Issuer, null!);
            Console.WriteLine("Test failed: No exception for null audience.");
        }
        catch (FirewallException)
        {
            Console.WriteLine("Test passed: Exception for null audience.");
        }

        try
        {
            _ = new JwtAuthenticator(SecretKey, Issuer, "");
            Console.WriteLine("Test failed: No exception for empty audience.");
        }
        catch (FirewallException)
        {
            Console.WriteLine("Test passed: Exception for empty audience.");
        }
    }

    public static void GenerateTokenShouldReturnValidToken()
    {
        // Arrange
        var authenticator = new JwtAuthenticator(SecretKey, Issuer, Audience);
        var claims = new Dictionary<string, object> { { "userId", 12345 } };
        TimeSpan expiration = TimeSpan.FromMinutes(5);

        // Act
        string token = authenticator.GenerateToken(claims, expiration);

        // Assert
        if (!string.IsNullOrWhiteSpace(token) && token.Split('.').Length == 3)
        {
            Console.WriteLine("Test passed: Valid token generated.");
        }
        else
        {
            Console.WriteLine("Test failed: Invalid token generated.");
        }
    }

    public static void ValidateTokenShouldReturnTrueForValidToken()
    {
        // Arrange
        var authenticator = new JwtAuthenticator(SecretKey, Issuer, Audience);
        var claims = new Dictionary<string, object> { { "userId", 12345 } };
        TimeSpan expiration = TimeSpan.FromMinutes(5);
        string token = authenticator.GenerateToken(claims, expiration);

        // Act
        bool isValid = authenticator.ValidateToken(token);

        // Assert
        if (isValid)
        {
            Console.WriteLine("Test passed: Token is valid.");
        }
        else
        {
            Console.WriteLine("Test failed: Token is invalid.");
        }
    }

    public static void ValidateTokenShouldReturnFalseForInvalidSignature()
    {
        // Arrange
        var authenticator = new JwtAuthenticator(SecretKey, Issuer, Audience);
        var claims = new Dictionary<string, object> { { "userId", 12345 } };
        TimeSpan expiration = TimeSpan.FromMinutes(5);
        string token = authenticator.GenerateToken(claims, expiration);

        // Tamper with token signature
        string tamperedToken = string.Concat(token.AsSpan(0, token.LastIndexOf('.') + 1), "tampered");

        // Act
        bool isValid = authenticator.ValidateToken(tamperedToken);

        // Assert
        if (!isValid)
        {
            Console.WriteLine("Test passed: Token with invalid signature is recognized.");
        }
        else
        {
            Console.WriteLine("Test failed: Token with invalid signature is not recognized.");
        }
    }

    public static void ValidateTokenShouldReturnFalseForExpiredToken()
    {
        // Arrange
        var authenticator = new JwtAuthenticator(SecretKey, Issuer, Audience);
        var claims = new Dictionary<string, object> { { "userId", 12345 } };
        TimeSpan expiration = TimeSpan.FromSeconds(-1); // Expired token
        string token = authenticator.GenerateToken(claims, expiration);

        // Act
        bool isValid = authenticator.ValidateToken(token);

        // Assert
        if (!isValid)
        {
            Console.WriteLine("Test passed: Expired token is recognized.");
        }
        else
        {
            Console.WriteLine("Test failed: Expired token is not recognized.");
        }
    }

    public static void ValidateTokenShouldReturnFalseForMalformedToken()
    {
        // Arrange
        var authenticator = new JwtAuthenticator(SecretKey, Issuer, Audience);
        string malformedToken = "invalid.token.format";

        // Act
        bool isValid = authenticator.ValidateToken(malformedToken);

        // Assert
        if (!isValid)
        {
            Console.WriteLine("Test passed: Malformed token is recognized.");
        }
        else
        {
            Console.WriteLine("Test failed: Malformed token is not recognized.");
        }
    }
}