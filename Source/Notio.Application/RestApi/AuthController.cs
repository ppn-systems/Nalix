using Microsoft.EntityFrameworkCore;
using Notio.Cryptography.Hash;
using Notio.Database;
using Notio.Database.Entities;
using Notio.Web.Enums;
using Notio.Web.Http;
using Notio.Web.Routing;
using Notio.Web.Security;
using Notio.Web.WebApi;
using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;

namespace Notio.Application.RestApi;

internal class AuthController(NotioContext context) : WebApiController
{
    private class UserWeb
    {
        public string Username { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;

        public string Password { get; set; } = string.Empty;
    }

    public static readonly JwtToken JwtToken = new(
        "Win^)Nqn7`P=E?N88YR?^QhM*>FyXR0B",
        "9Tcpg:>K|_6d,\\X&RfSh6dotqoT{fO.v",
        "b,uRaB(Gs\"B[>N,X>9|v(.T;,<h0f(HL"
    );

    private readonly NotioContext _context = context;

    [Route(HttpVerbs.Post, "/auth/register")]
    public async Task<object> RegisterUser()
    {
        User userData = await HttpContext.GetRequestDataAsync<User>();
        // Kiểm tra username và email có tồn tại chưa
        if (await _context.Users.AnyAsync(u => u.Username == userData.Username))
        {
            Response.StatusCode = (int)HttpStatusCode.BadRequest;
            return new { Message = "Username đã tồn tại" };
        }
        if (await _context.Users.AnyAsync(u => u.Email == userData.Email))
        {
            Response.StatusCode = (int)HttpStatusCode.BadRequest;
            return new { Message = "Email đã tồn tại" };
        }

        await _context.Users.AddAsync(new User
        {
            Username = userData.Username,
            Email = userData.Email,
            DisplayName = "null",
            PasswordHash = SecurePasswordHasher.Hash(userData.PasswordHash),
            AvatarUrl = "null"
        });

        await _context.SaveChangesAsync();
        Response.StatusCode = (int)HttpStatusCode.Created;
        return new { Message = "Success" };
    }

    [Route(HttpVerbs.Post, "/auth/login")]
    public async Task<object> LoginUser()
    {
        UserWeb userData = await HttpContext.GetRequestDataAsync<UserWeb>();
        User? user = await _context.Users.FirstOrDefaultAsync(u => u.Username == userData.Username);

        if (user == null)
        {
            Response.StatusCode = (int)HttpStatusCode.Unauthorized;
            return new { message = "User not found" };
        }
        if (!SecurePasswordHasher.Verify(userData.Password, user.PasswordHash))
        {
            Response.StatusCode = (int)HttpStatusCode.Unauthorized;
            return new { message = "Invalid password" };
        }

        var claims = new Dictionary<string, object>
        {
            { "sub", user.UserId },
            { "username", user.Username },
            { "email", user.Email }
        };

        string token = JwtToken.GenerateToken(claims, TimeSpan.FromHours(1));

        Response.StatusCode = (int)HttpStatusCode.OK;
        Response.Headers.Add("Authorization", $"Bearer {token}");

        return new { message = "Login successful" };
    }

    [Route(HttpVerbs.Post, "/auth/refresh-token")]
    public object RefreshToken()
    {
        string? authorizationHeader = HttpContext.Request.Headers["Authorization"];
        if (string.IsNullOrEmpty(authorizationHeader))
        {
            Response.StatusCode = (int)HttpStatusCode.BadRequest;
            return new { message = "Refresh token is required" };
        }

        string refreshToken = authorizationHeader.Replace("Bearer ", "");

        try
        {
            var claims = JwtToken.DecodeToken(refreshToken);
            // Kiểm tra thời gian hết hạn token
            if (claims.TryGetValue("exp", out object? value)
                && long.TryParse(value.ToString(), out var exp) && exp < DateTimeOffset.UtcNow.ToUnixTimeSeconds())
            {
                Response.StatusCode = (int)HttpStatusCode.Unauthorized;
                return new { message = "Token expired" };
            }

            // Tạo lại token mới
            var newToken = JwtToken.GenerateToken(claims, TimeSpan.FromHours(1)); // Thời gian mới cho token

            Response.StatusCode = (int)HttpStatusCode.OK;
            return new { message = "Token refreshed", Token = newToken };
        }
        catch (Exception ex)
        {
            Response.StatusCode = (int)HttpStatusCode.BadRequest;
            return new { message = "Invalid token", error = ex.Message };
        }
    }
}