using Notio.Http.Attributes;
using Notio.Http.Core;
using Notio.Network.Firewall;
using System;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using static Notio.Http.HttpExtensions;

[ApiController]
internal sealed class AuthController
{
    private const string ADMIN_USERNAME = "admin";
    private const string ADMIN_PASSWORD = "password";
    private const string ADMIN_ROLE = "admin";
    private const string USER_ROLE = "user";
    private const string AUTH_SCHEME = "Bearer ";

    private record struct LoginRequest(string? Username, string? Password);
    private record struct RefreshTokenRequest(string? RefreshToken);
    private record struct TokenResponse(string Token);
    private record struct UserInfoResponse(string Username, string Role);

    private readonly JwtAuthenticator _jwtAuthenticator = new(
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

    [Route("/api/login", HttpMethodType.POST)]
    public async Task Login(HttpContext context)
    {
        var request = await context.Request.InputStream.DeserializeRequestAsync<LoginRequest>();

        var response = request switch
        {
            { Username: null or "", Password: null or "" } =>
                new ApiResponse<TokenResponse>(HttpStatusCode.BadRequest, Error: "Missing credentials"),

            { Username: ADMIN_USERNAME, Password: ADMIN_PASSWORD } =>
                new ApiResponse<TokenResponse>(HttpStatusCode.OK, GenerateTokenResponse(ADMIN_USERNAME, USER_ROLE)),

            _ => new ApiResponse<TokenResponse>(HttpStatusCode.Unauthorized, Error: "Invalid credentials")
        };

        await context.Response.SendResponseAsync(response);
    }

    [Route("/api/refresh-token", HttpMethodType.POST)]
    public async Task RefreshToken(HttpContext context)
    {
        var request = await context.Request.InputStream.DeserializeRequestAsync<RefreshTokenRequest>();

        if (request is not { RefreshToken: { } token } || !_jwtAuthenticator.ValidateToken(token))
        {
            await context.Response.SendResponseAsync(
                new ApiResponse<TokenResponse>(HttpStatusCode.Unauthorized, Error: "Invalid refresh token"));
            return;
        }

        var claims = JwtAuthenticator.DecodeToken(token);
        if (!claims.TryGetValue("sub", out var sub) || !claims.TryGetValue("role", out var role))
        {
            await context.Response.SendResponseAsync(
                new ApiResponse<TokenResponse>(HttpStatusCode.Unauthorized, Error: "Invalid token claims"));
            return;
        }

        await context.Response.SendResponseAsync(
            new ApiResponse<TokenResponse>(
                HttpStatusCode.OK,
                GenerateTokenResponse(sub.ToString()!, role.ToString()!)));
    }

    [Route("/api/protected", HttpMethodType.GET)]
    public async Task ProtectedEndpoint(HttpContext context)
    {
        var authHeader = context.Request.Headers["Authorization"];

        if (!authHeader?.StartsWith(AUTH_SCHEME) ?? true)
        {
            await context.Response.SendResponseAsync(
                new ApiResponse<UserInfoResponse>(HttpStatusCode.BadRequest, Error: "Missing authorization header"));
            return;
        }

        if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith(AUTH_SCHEME))
        {
            await context.Response.SendResponseAsync(
                new ApiResponse<UserInfoResponse>(HttpStatusCode.BadRequest, Error: "Missing authorization header"));
            return;
        }

        string token = authHeader[AUTH_SCHEME.Length..];

        if (!_jwtAuthenticator.ValidateToken(token))
        {
            await context.Response.SendResponseAsync(
                new ApiResponse<UserInfoResponse>(HttpStatusCode.Unauthorized, Error: "Invalid token"));
            return;
        }

        var claims = JwtAuthenticator.DecodeToken(token);
        if (!claims.TryGetValue("sub", out var username) ||
            !claims.TryGetValue("role", out var role) ||
            role?.ToString() != ADMIN_ROLE)
        {
            await context.Response.SendResponseAsync(
                new ApiResponse<UserInfoResponse>(HttpStatusCode.Forbidden, Error: "Insufficient privileges"));
            return;
        }

        await context.Response.SendResponseAsync(
            new ApiResponse<UserInfoResponse>(
                HttpStatusCode.OK,
                new UserInfoResponse(username.ToString()!, role.ToString()!)));
    }
}