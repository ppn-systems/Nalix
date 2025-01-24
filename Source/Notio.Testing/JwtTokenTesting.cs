using Notio.Network.Security;
using System;
using System.Collections.Generic;

namespace Notio.Testing;

public sealed class JwtTokenTesting
{
    private const string SecretKey = "supersecretkey123";
    private const string Issuer = "testIssuer";
    private const string Audience = "testAudience";

    public static void Main()
    {
        var tests = new Action[]
        {
            GenerateTokenShouldReturnValidToken,
            ValidateTokenShouldReturnTrueForValidToken,
            ValidateTokenShouldReturnFalseForInvalidSignature,
            ValidateTokenShouldReturnFalseForExpiredToken,
            ValidateTokenShouldReturnFalseForMalformedToken
        };

        foreach (var test in tests)
        {
            try
            {
                test.Invoke();
                Console.WriteLine($"{test.Method.Name}: Passed");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"{test.Method.Name}: Failed - {ex.Message}");
            }
        }

        Console.WriteLine("All tests completed.");
    }

    private static void GenerateTokenShouldReturnValidToken()
    {
        var authenticator = new JwtToken(SecretKey, Issuer, Audience);
        var claims = new Dictionary<string, object> { { "userId", 12345 } };

        string token = authenticator.GenerateToken(claims, TimeSpan.FromMinutes(5));

        if (string.IsNullOrWhiteSpace(token) || token.Split('.').Length != 3)
        {
            throw new Exception("Invalid token generated.");
        }
    }

    private static void ValidateTokenShouldReturnTrueForValidToken()
    {
        var authenticator = new JwtToken(SecretKey, Issuer, Audience);
        var claims = new Dictionary<string, object> { { "userId", 12345 } };
        string token = authenticator.GenerateToken(claims, TimeSpan.FromMinutes(5));

        if (!authenticator.ValidateToken(token, out _))
        {
            throw new Exception("Token validation failed.");
        }
    }

    private static void ValidateTokenShouldReturnFalseForInvalidSignature()
    {
        var authenticator = new JwtToken(SecretKey, Issuer, Audience);
        var claims = new Dictionary<string, object> { { "userId", 12345 } };
        string token = authenticator.GenerateToken(claims, TimeSpan.FromMinutes(5));

        string tamperedToken = string.Concat(token.AsSpan(0, token.LastIndexOf('.') + 1), "tampered");

        if (authenticator.ValidateToken(tamperedToken, out _))
        {
            throw new Exception("Invalid signature not recognized.");
        }
    }

    private static void ValidateTokenShouldReturnFalseForExpiredToken()
    {
        var authenticator = new JwtToken(SecretKey, Issuer, Audience);
        var claims = new Dictionary<string, object> { { "userId", 12345 } };
        string token = authenticator.GenerateToken(claims, TimeSpan.FromSeconds(-1));

        if (authenticator.ValidateToken(token, out _))
        {
            throw new Exception("Expired token not recognized.");
        }
    }

    private static void ValidateTokenShouldReturnFalseForMalformedToken()
    {
        var authenticator = new JwtToken(SecretKey, Issuer, Audience);
        string malformedToken = "invalid.token.format";

        if (authenticator.ValidateToken(malformedToken, out _))
        {
            throw new Exception("Malformed token not recognized.");
        }
    }
}