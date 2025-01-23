using System;
using System.Collections.Generic;
using Notio.Network.Security;
using Notio.Common.Exceptions;

namespace Notio.Testing;

public sealed class JwtTokenTesting
{
    private const string SecretKey = "supersecretkey123";
    private const string Issuer = "testIssuer";
    private const string Audience = "testAudience";

    public static void Main()
    {
        GenerateTokenShouldReturnValidToken();
        ValidateTokenShouldReturnTrueForValidToken();
        ValidateTokenShouldReturnFalseForInvalidSignature();
        ValidateTokenShouldReturnFalseForExpiredToken();
        ValidateTokenShouldReturnFalseForMalformedToken();

        Console.WriteLine("All tests completed.");
    }
   
    public static void GenerateTokenShouldReturnValidToken()
    {
        // Arrange
        var authenticator = new JwtToken(SecretKey, Issuer, Audience);
        var claims = new Dictionary<string, object> { { "userId", 12345 } };
        TimeSpan expiration = TimeSpan.FromMinutes(5);

        // Act
        string token = authenticator.GenerateToken(claims, expiration);

        // Assert
        if (!string.IsNullOrWhiteSpace(token) && token.Split('.').Length == 3)
        {
            Console.WriteLine("Testing passed: Valid token generated.");
        }
        else
        {
            Console.WriteLine("Testing failed: Invalid token generated.");
        }
    }

    public static void ValidateTokenShouldReturnTrueForValidToken()
    {
        // Arrange
        var authenticator = new JwtToken(SecretKey, Issuer, Audience);
        var claims = new Dictionary<string, object> { { "userId", 12345 } };
        TimeSpan expiration = TimeSpan.FromMinutes(5);
        string token = authenticator.GenerateToken(claims, expiration);

        // Act
        bool isValid = authenticator.ValidateToken(token, out _);

        // Assert
        if (isValid)
        {
            Console.WriteLine("Testing passed: Token is valid.");
        }
        else
        {
            Console.WriteLine("Testing failed: Token is invalid.");
        }
    }

    public static void ValidateTokenShouldReturnFalseForInvalidSignature()
    {
        // Arrange
        var authenticator = new JwtToken(SecretKey, Issuer, Audience);
        var claims = new Dictionary<string, object> { { "userId", 12345 } };
        TimeSpan expiration = TimeSpan.FromMinutes(5);
        string token = authenticator.GenerateToken(claims, expiration);

        // Tamper with token signature
        string tamperedToken = string.Concat(token.AsSpan(0, token.LastIndexOf('.') + 1), "tampered");

        // Act
        bool isValid = authenticator.ValidateToken(tamperedToken, out _);

        // Assert
        if (!isValid)
        {
            Console.WriteLine("Testing passed: Token with invalid signature is recognized.");
        }
        else
        {
            Console.WriteLine("Testing failed: Token with invalid signature is not recognized.");
        }
    }

    public static void ValidateTokenShouldReturnFalseForExpiredToken()
    {
        // Arrange
        var authenticator = new JwtToken(SecretKey, Issuer, Audience);
        var claims = new Dictionary<string, object> { { "userId", 12345 } };
        TimeSpan expiration = TimeSpan.FromSeconds(-1); // Expired token
        string token = authenticator.GenerateToken(claims, expiration);

        // Act
        bool isValid = authenticator.ValidateToken(token, out _);

        // Assert
        if (!isValid)
        {
            Console.WriteLine("Testing passed: Expired token is recognized.");
        }
        else
        {
            Console.WriteLine("Testing failed: Expired token is not recognized.");
        }
    }

    public static void ValidateTokenShouldReturnFalseForMalformedToken()
    {
        // Arrange
        var authenticator = new JwtToken(SecretKey, Issuer, Audience);
        string malformedToken = "invalid.token.format";

        // Act
        bool isValid = authenticator.ValidateToken(malformedToken, out _);

        // Assert
        if (!isValid)
        {
            Console.WriteLine("Testing passed: Malformed token is recognized.");
        }
        else
        {
            Console.WriteLine("Testing failed: Malformed token is not recognized.");
        }
    }
}