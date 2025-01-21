using Notio.Http.Attributes;
using Notio.Http.Core;
using Notio.Network.Firewall;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using System;
using System.Net;
using System.Net.Http;

[ApiController]
internal class AuthController : HttpController
{
    public class RefreshTokenRequest
    {
        public string? RefreshToken { get; set; }
    }

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

    // Đăng nhập API (cung cấp token JWT)
    [Route("/api/login", HttpMethodType.POST)]
    public async Task<HttpResponse> Login(HttpContext context)
    {
        using var reader = new StreamReader(context.Request.InputStream);
        var body = await reader.ReadToEndAsync();

        var credentials = JsonSerializer.Deserialize<LoginRequest>(body);

        if (credentials == null || string.IsNullOrWhiteSpace(credentials.Username) || string.IsNullOrWhiteSpace(credentials.Password))
        {
            return new HttpResponse(HttpStatusCode.BadRequest, null, "Missing username or password", null);
        }

        if (credentials.Username == "admin" && credentials.Password == "password")
        {
            var claims = new Dictionary<string, object>
            {
                { "sub", credentials.Username },
                { "role", "user" }
            };

            string token = _jwtAuthenticator.GenerateToken(claims, TimeSpan.FromHours(1));

            return new HttpResponse(
                HttpStatusCode.OK,
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

    // API để cấp lại token mới
    [Route("/api/refresh-token", HttpMethodType.POST)]
    public async Task<HttpResponse> RefreshToken(HttpContext context)
    {
        using var reader = new StreamReader(context.Request.InputStream);
        var body = await reader.ReadToEndAsync();

        RefreshTokenRequest? request = JsonSerializer.Deserialize<RefreshTokenRequest>(body);

        if (request == null || string.IsNullOrWhiteSpace(request.RefreshToken))
            return new HttpResponse(HttpStatusCode.BadRequest, null, "Missing refresh token", null);

        // Kiểm tra tính hợp lệ của refresh token
        bool isValid = _jwtAuthenticator.ValidateToken(request.RefreshToken);
        if (!isValid)
            return new HttpResponse(HttpStatusCode.Unauthorized, null, "Invalid refresh token", null);

        // Nếu refresh token hợp lệ, cấp lại token mới
        var claims = JwtAuthenticator.DecodeToken(request.RefreshToken);

        if (claims.TryGetValue("sub", out object? sub) &&
            claims.TryGetValue("role", out object? role))
        {
            var newClaims = new Dictionary<string, object>
            {
                { "sub", sub },
                { "role", role }
            };

            string newToken = _jwtAuthenticator.GenerateToken(newClaims, TimeSpan.FromHours(1));

            return new HttpResponse(HttpStatusCode.OK, new { Token = newToken }, null, null);
        }

        return new HttpResponse(HttpStatusCode.Unauthorized, null, "Invalid token claims", null);
    }

    // API bảo vệ (yêu cầu JWT hợp lệ với role "admin")
    [Route("/api/protected", HttpMethodType.GET)]
    public Task<HttpResponse> ProtectedEndpoint(HttpContext context)
    {
        if (context.Request.Headers["Authorization"] is string authorizationHeader && authorizationHeader.StartsWith("Bearer "))
        {
            string token = authorizationHeader[7..];

            bool isValid = _jwtAuthenticator.ValidateToken(token);
            Console.WriteLine($"Token is valid: {isValid}");

            if (isValid)
            {
                var claims = JwtAuthenticator.DecodeToken(token);
                Console.WriteLine($"Claims: {JsonSerializer.Serialize(claims)}");

                if (claims.TryGetValue("sub", out var sub) && sub is string username &&
                    claims.TryGetValue("role", out var role) && role is string userRole)
                {
                    if (userRole == "admin")
                    {
                        return Task.FromResult(new HttpResponse(HttpStatusCode.OK, new { Username = username, Role = userRole }, null, null));
                    }

                    return Task.FromResult(new HttpResponse(HttpStatusCode.Forbidden, null, "Access denied: Insufficient privileges", null));
                }

                return Task.FromResult(new HttpResponse(HttpStatusCode.Unauthorized, null, "Invalid token", null));
            }
            
            return Task.FromResult(new HttpResponse(HttpStatusCode.Unauthorized, null, "Invalid token", null));
        }

        return Task.FromResult(new HttpResponse(HttpStatusCode.BadRequest, null, "Authorization header missing", null));
    }
}