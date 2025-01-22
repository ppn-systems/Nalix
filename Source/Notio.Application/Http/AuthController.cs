using Notio.Network.Http;
using Notio.Network.Http.Attributes;
using Notio.Network.Security;
using System;
using System.Net;
using System.Threading.Tasks;

namespace Notio.Application.Http;

[ApiController]
internal sealed class AuthController
{
    private const string ADMIN_USERNAME = "admin";
    private const string ADMIN_PASSWORD = "123";
    private const string ADMIN_ROLE = "admin";
    private const string USER_ROLE = "user";
    private const string AUTH_SCHEME = "Bearer ";

    private record struct LoginRequest(string? Username, string? Password);
    private record struct RefreshTokenRequest(string? RefreshToken);
    private record struct TokenResponse(string Token);
    private record struct UserInfoResponse(string Username, string Role);
    private record struct AuthenticationResponse(bool IsAuthenticated, string? Username = null, string? Role = null, string? Error = null);

    private readonly JwtToken _jwtAuthenticator = new(
        secretKey: "your_secret_key",
        issuer: "your_issuer",
        audience: "your_audience");

    private TokenResponse GenerateTokenResponse(string username, string role) =>
        new(_jwtAuthenticator.GenerateToken(
            new()
            {
                ["sub"] = username,
                ["role"] = role
            },
            TimeSpan.FromHours(1)));

    private async Task<AuthenticationResponse> TryAuthenticate(HttpContext context)
    {
        var authHeader = context.Request.Headers["Authorization"];
        if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith(AUTH_SCHEME))
        {
            await context.Response.WriteErrorResponseAsync(HttpStatusCode.BadRequest, "Missing or invalid authorization header");
            return new AuthenticationResponse(false, Error: "Invalid header");
        }

        var token = authHeader[AUTH_SCHEME.Length..];
        if (!_jwtAuthenticator.ValidateToken(token, out _))
        {
            await context.Response.WriteErrorResponseAsync(HttpStatusCode.Unauthorized, "Invalid token");
            return new AuthenticationResponse(false, Error: "Invalid token");
        }

        var claims = JwtToken.DecodeToken(token);
        if (!claims.TryGetValue("sub", out var sub) || !claims.TryGetValue("role", out var roleClaim))
        {
            await context.Response.WriteErrorResponseAsync(HttpStatusCode.Unauthorized, "Invalid token claims");
            return new AuthenticationResponse(false, Error: "Invalid claims");
        }

        return new AuthenticationResponse(true, sub.ToString()!, roleClaim.ToString()!);
    }

    [Route("/api/login", HttpMethodType.POST)]
    public async Task Login(HttpContext context)
    {
        LoginRequest request = await context.Request.InputStream.DeserializeRequestAsync<LoginRequest>();

        if (string.IsNullOrEmpty(request.Username) || string.IsNullOrEmpty(request.Password))
        {
            await context.Response.WriteErrorResponseAsync(HttpStatusCode.BadRequest, "Missing credentials");
            return;
        }

        if (request.Username == ADMIN_USERNAME && request.Password == ADMIN_PASSWORD)
        {
            await context.Response.WriteJsonResponseAsync(HttpStatusCode.OK,
                GenerateTokenResponse(ADMIN_USERNAME, ADMIN_ROLE));
            return;
        }

        await context.Response.WriteErrorResponseAsync(HttpStatusCode.Unauthorized, "Invalid credentials");
    }

    [Route("/api/refresh-token", HttpMethodType.POST)]
    public async Task RefreshToken(HttpContext context)
    {
        var request = await context.Request.InputStream.DeserializeRequestAsync<RefreshTokenRequest>();

        if (request is not { RefreshToken: { } token } || !_jwtAuthenticator.ValidateToken(token, out _))
        {
            await context.Response.WriteErrorResponseAsync(HttpStatusCode.Unauthorized, "Invalid refresh token");
            return;
        }

        var claims = JwtToken.DecodeToken(token);
        if (!claims.TryGetValue("sub", out var sub) || !claims.TryGetValue("role", out var role))
        {
            await context.Response.WriteErrorResponseAsync(HttpStatusCode.Unauthorized, "Invalid token claims");
            return;
        }

        await context.Response.WriteJsonResponseAsync(
            HttpStatusCode.OK,
            GenerateTokenResponse(sub.ToString()!, role.ToString()!));
    }

    [Route("/api/protected", HttpMethodType.GET)]
    public async Task ProtectedEndpoint(HttpContext context)
    {
        var authResult = await TryAuthenticate(context);
        if (!authResult.IsAuthenticated)
            return;

        if (authResult.Role != ADMIN_ROLE)
        {
            await context.Response.WriteErrorResponseAsync(HttpStatusCode.Forbidden, "Insufficient privileges");
            return;
        }

        await context.Response.WriteJsonResponseAsync(
            HttpStatusCode.OK,
            new UserInfoResponse(authResult.Username!, authResult.Role!));
    }
}