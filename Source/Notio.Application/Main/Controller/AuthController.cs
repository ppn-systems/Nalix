using Notio.Http.Attributes;
using Notio.Http.Core;
using Notio.Http.Enums;
using Notio.Network.Firewall;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace Notio.Application.Main.Controller;

[ApiController]
internal class AuthController : HttpController
{
    public class LoginRequest
    {
        public string? Username { get; set; }
        public string? Password { get; set; }
    }

    private readonly JwtAuthenticator _jwtAuthenticator;

    public AuthController()
    {
        // Khởi tạo JwtAuthenticator với các giá trị cố định
        string secretKey = "your_secret_key";
        string issuer = "your_issuer";
        string audience = "your_audience";
        _jwtAuthenticator = new JwtAuthenticator(secretKey, issuer, audience);
    }

    [Route("/api/login", HttpMethod.POST)]
    public async Task<HttpResponse> Login(HttpContext context)
    {
        using var reader = new StreamReader(context.Request.InputStream);
        var body = await reader.ReadToEndAsync();

        var credentials = JsonSerializer.Deserialize<LoginRequest>(body);

        if (credentials != null && credentials.Username == "admin" && credentials.Password == "password")
        {
            // Tạo token với thông tin người dùng
            var claims = new Dictionary<string, object>
                {
                    { "sub", credentials.Username },
                    { "role", "user" }
                };

            string token = _jwtAuthenticator.GenerateToken(claims, TimeSpan.FromHours(1));

            return new HttpResponse(
                HttpStatusCode.Ok,
                new { Token = token },
                null,
                null
            );
        }

        return new HttpResponse(
            HttpStatusCode.Unauthorized,
            null,
            "Invalid credentials",
            null
        );
    }

    [Route("/api/protected", HttpMethod.GET)]
    public Task<HttpResponse> Protected(HttpContext context)
    {
        if (context.Request.Headers["Authorization"] 
            is string authorizationHeader && authorizationHeader.StartsWith("Bearer "))
        {
            string token = authorizationHeader[7..];

            if (_jwtAuthenticator.ValidateToken(token))
                return Task.FromResult(new HttpResponse(HttpStatusCode.Ok, null, "Access granted.", null));
            else
                return Task.FromResult(new HttpResponse(HttpStatusCode.Unauthorized, null, "Invalid or expired token.", null));
        }

        return Task.FromResult(new HttpResponse(HttpStatusCode.Unauthorized, null, "Authorization header missing or malformed.", null));
    }
}