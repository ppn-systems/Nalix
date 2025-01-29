using Notio.Common;
using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Notio.Network.Security;

public sealed class JwtToken
{
    private readonly string _issuer;
    private readonly string _audience;
    private readonly HMACSHA256 _hmac;

    public JwtToken(string secretKey, string issuer, string audience)
    {
        if (string.IsNullOrWhiteSpace(secretKey))
            throw new InternalErrorException("Secret key cannot be null or empty.", nameof(secretKey));

        if (string.IsNullOrWhiteSpace(issuer))
            throw new InternalErrorException("Issuer cannot be null or empty.", nameof(issuer));

        if (string.IsNullOrWhiteSpace(audience))
            throw new InternalErrorException("Audience cannot be null or empty.", nameof(audience));

        _issuer = issuer;
        _audience = audience;
        _hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secretKey));
    }

    public string GenerateToken(Dictionary<string, object> claims, TimeSpan expiration)
    {
        ArgumentNullException.ThrowIfNull(claims);

        // Header
        var header = new { alg = "HS256", typ = "JWT" };
        string headerBase64 = ConvertToBase64Url(JsonSerializer.SerializeToUtf8Bytes(header));

        // Payload
        claims["iss"] = _issuer;
        claims["aud"] = _audience;
        claims["exp"] = DateTimeOffset.UtcNow.Add(expiration).ToUnixTimeSeconds();
        claims["iat"] = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        string payloadBase64 = ConvertToBase64Url(JsonSerializer.SerializeToUtf8Bytes(claims));

        // Signature
        byte[] signature = _hmac.ComputeHash(Encoding.UTF8.GetBytes($"{headerBase64}.{payloadBase64}"));
        string signatureBase64 = ConvertToBase64Url(signature);

        return $"{headerBase64}.{payloadBase64}.{signatureBase64}";
    }

    public bool ValidateToken(string token, out Dictionary<string, object>? claims)
    {
        claims = null;
        if (string.IsNullOrWhiteSpace(token))
            return false;

        try
        {
            string[] parts = token.Split('.');
            if (parts.Length != 3) return false;

            // Validate Signature
            byte[] computedSignature = _hmac.ComputeHash(Encoding.UTF8.GetBytes($"{parts[0]}.{parts[1]}"));
            if (ConvertToBase64Url(computedSignature) != parts[2]) return false;

            // Validate Payload
            string payloadJson = Encoding.UTF8.GetString(Convert.FromBase64String(parts[1]));
            var payload = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(payloadJson);

            if (payload == null || !payload.TryGetValue("exp", out JsonElement expValue)) return false;

            if (DateTimeOffset.UtcNow.ToUnixTimeSeconds() > expValue.GetInt64()) return false;

            claims = DeserializeClaims(payload);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public static Dictionary<string, object> DecodeToken(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
            throw new InternalErrorException("Token cannot be null or empty.", nameof(token));

        try
        {
            string[] parts = token.Split('.');
            if (parts.Length != 3)
                throw new InternalErrorException("Invalid token format.", nameof(token));

            // Decode Payload
            string payloadJson = Encoding.UTF8.GetString(Convert.FromBase64String(parts[1]));
            var payload = JsonSerializer.Deserialize<Dictionary<string, object>>(payloadJson)
                ?? throw new InternalErrorException("Invalid payload in token.", nameof(token));

            return payload;
        }
        catch (Exception ex)
        {
            throw new InternalErrorException("Failed to decode token.", ex);
        }
    }

    private static string ConvertToBase64Url(byte[] input)
        => Convert.ToBase64String(input).TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

    private static Dictionary<string, object> DeserializeClaims(Dictionary<string, JsonElement> jsonElements)
    {
        Dictionary<string, object> claims = [];
        foreach (var kvp in jsonElements)
        {
            claims[kvp.Key] = kvp.Value.ValueKind switch
            {
                JsonValueKind.String => kvp.Value.GetString() ?? string.Empty,
                JsonValueKind.Number => kvp.Value.TryGetInt64(out var longValue) ? longValue : kvp.Value.GetDouble(),
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                _ => kvp.Value.GetRawText(),
            };
        }
        return claims;
    }
}