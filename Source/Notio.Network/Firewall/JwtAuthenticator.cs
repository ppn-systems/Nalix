using Notio.Common.Exceptions;
using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Notio.Network.Firewall;

public class JwtAuthenticator
{
    private readonly string _issuer;
    private readonly string _audience;
    private readonly HMACSHA256 _hmac;

    public JwtAuthenticator(string secretKey, string issuer, string audience)
    {
        if (string.IsNullOrWhiteSpace(secretKey)) 
            throw new FirewallException("Secret key cannot be null or empty.", nameof(secretKey));

        if (string.IsNullOrWhiteSpace(issuer)) 
            throw new FirewallException("Issuer cannot be null or empty.", nameof(issuer));

        if (string.IsNullOrWhiteSpace(audience)) 
            throw new FirewallException("Audience cannot be null or empty.", nameof(audience));

        _issuer = issuer;
        _audience = audience;
        _hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secretKey));
    }

    public string GenerateToken(Dictionary<string, object> claims, TimeSpan expiration)
    {
        ArgumentNullException.ThrowIfNull(claims);

        // Header
        var header = new { alg = "HS256", typ = "JWT" };
        string headerBase64 = Convert.ToBase64String(JsonSerializer.SerializeToUtf8Bytes(header));

        // Payload
        claims["iss"] = _issuer;
        claims["aud"] = _audience;
        claims["exp"] = DateTimeOffset.UtcNow.Add(expiration).ToUnixTimeSeconds();
        claims["iat"] = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        string payloadBase64 = Convert.ToBase64String(JsonSerializer.SerializeToUtf8Bytes(claims));

        // Signature
        byte[] signature = _hmac.ComputeHash(Encoding.UTF8.GetBytes($"{headerBase64}.{payloadBase64}"));
        string signatureBase64 = Convert.ToBase64String(signature);

        return $"{headerBase64}.{payloadBase64}.{signatureBase64}";
    }

    public bool ValidateToken(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
            return false;

        try
        {
            string[] parts = token.Split('.');
            if (parts.Length != 3) return false;

            // Validate Signature
            byte[] computedSignature = _hmac.ComputeHash(Encoding.UTF8.GetBytes($"{parts[0]}.{parts[1]}"));
            string computedSignatureBase64 = Convert.ToBase64String(computedSignature);

            if (computedSignatureBase64 != parts[2]) return false;

            // Validate Payload
            string payloadJson = Encoding.UTF8.GetString(Convert.FromBase64String(parts[1]));
            var payload = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(payloadJson);

            if (payload == null || !payload.TryGetValue("exp", out JsonElement expValue)) return false;       

            if (DateTimeOffset.UtcNow.ToUnixTimeSeconds() > expValue.GetInt64()) return false;

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
            throw new FirewallException("Token cannot be null or empty.", nameof(token));

        try
        {
            string[] parts = token.Split('.');
            if (parts.Length != 3)
                throw new FirewallException("Invalid token format.", nameof(token));

            // Decode Payload
            string payloadJson = Encoding.UTF8.GetString(Convert.FromBase64String(parts[1]));
            var payload = JsonSerializer.Deserialize<Dictionary<string, object>>(payloadJson) 
                ?? throw new FirewallException("Invalid payload in token.", nameof(token));

            return payload;
        }
        catch (Exception ex)
        {
            throw new FirewallException("Failed to decode token.", ex);
        }
    }
}