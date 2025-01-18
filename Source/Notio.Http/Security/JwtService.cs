using System.Collections.Generic;
using System;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Notio.Http.Security;

public class JwtService(string secretKey, string issuer, string audience)
{
    private readonly string _issuer = issuer;
    private readonly string _audience = audience;
    private readonly HMACSHA256 _hmac = new(Encoding.UTF8.GetBytes(secretKey));

    public string GenerateToken(Dictionary<string, object> claims, TimeSpan expiration)
    {
        var header = new { alg = "HS256", typ = "JWT" };
        var headerBase64 = Convert.ToBase64String(JsonSerializer.SerializeToUtf8Bytes(header));

        claims["iss"] = _issuer;
        claims["aud"] = _audience;
        claims["exp"] = DateTimeOffset.UtcNow.Add(expiration).ToUnixTimeSeconds();
        claims["iat"] = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var payloadBase64 = Convert.ToBase64String(JsonSerializer.SerializeToUtf8Bytes(claims));

        var signature = _hmac.ComputeHash(Encoding.UTF8.GetBytes($"{headerBase64}.{payloadBase64}"));
        var signatureBase64 = Convert.ToBase64String(signature);

        return $"{headerBase64}.{payloadBase64}.{signatureBase64}";
    }

    public bool ValidateToken(string token)
    {
        try
        {
            var parts = token.Split('.');
            if (parts.Length != 3) return false;

            var computedSignature = _hmac.ComputeHash(Encoding.UTF8.GetBytes($"{parts[0]}.{parts[1]}"));
            var computedSignatureBase64 = Convert.ToBase64String(computedSignature);

            if (computedSignatureBase64 != parts[2]) return false;

            var payloadJson = Encoding.UTF8.GetString(Convert.FromBase64String(parts[1]));
            var payload = JsonSerializer.Deserialize<Dictionary<string, object>>(payloadJson);

            var exp = Convert.ToInt64(payload["exp"]);
            if (DateTimeOffset.UtcNow.ToUnixTimeSeconds() > exp) return false;

            return true;
        }
        catch
        {
            return false;
        }
    }
}