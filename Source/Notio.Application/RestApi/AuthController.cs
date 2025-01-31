using Microsoft.EntityFrameworkCore;
using Notio.Cryptography.Hash;
using Notio.Database;
using Notio.Database.Entities;
using Notio.Web.Enums;
using Notio.Web.Http.Extensions;
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

    public static readonly int ExpirationTimeHours = 999;

    private readonly NotioContext _context = context;

    [Route(HttpVerbs.Post, "/auth/register")]
    public async Task<object> RegisterUser()
    {
        try
        {
            UserWeb userData = await HttpContext.GetRequestDataAsync<UserWeb>();

            // Check if the username or email already exist
            if (await _context.Users.AnyAsync(u => u.Username == userData.Username))
            {
                Response.StatusCode = (int)HttpStatusCode.BadRequest;
                return new { Message = "Username already exists" };
            }

            if (await _context.Users.AnyAsync(u => u.Email == userData.Email))
            {
                Response.StatusCode = (int)HttpStatusCode.BadRequest;
                return new { Message = "Email already registered" };
            }

            // Add new user
            await _context.Users.AddAsync(new User
            {
                Username = userData.Username,
                Email = userData.Email,
                DisplayName = "Null",
                PasswordHash = PasswordProtector.Hash(userData.Password),
                AvatarUrl = "Null"
            });

            await _context.SaveChangesAsync();
            Response.StatusCode = (int)HttpStatusCode.Created;
            return new { Message = "Registration successful" };
        }
        catch (Exception ex)
        {
            Response.StatusCode = (int)HttpStatusCode.InternalServerError;
            return new { Message = "An error occurred", Error = ex.Message };
        }
    }

    [Route(HttpVerbs.Post, "/auth/login")]
    public async Task<object> LoginUser()
    {
        try
        {
            UserWeb userData = await HttpContext.GetRequestDataAsync<UserWeb>();
            User? user = await _context.Users.FirstOrDefaultAsync(u => u.Username == userData.Username);

            if (user == null)
            {
                Response.StatusCode = (int)HttpStatusCode.Unauthorized;
                return new { Message = "User not found" };
            }

            if (!PasswordProtector.Verify(userData.Password, user.PasswordHash))
            {
                Response.StatusCode = (int)HttpStatusCode.Unauthorized;
                return new { Message = "Invalid password" };
            }

            var claims = new Dictionary<string, object>
            {
                { "sub", user.UserId },
                { "username", user.Username },
                { "email", user.Email }
            };

            string token = JwtToken.GenerateToken(claims, TimeSpan.FromHours(ExpirationTimeHours));

            Response.StatusCode = (int)HttpStatusCode.OK;
            Response.Headers.Add("Authorization", $"Bearer {token}");

            return new { Message = "Login successful", Token = token };
        }
        catch (Exception ex)
        {
            Response.StatusCode = (int)HttpStatusCode.InternalServerError;
            return new { Message = "An error occurred", Error = ex.Message };
        }
    }

    [Route(HttpVerbs.Post, "/auth/refresh-token")]
    public object RefreshToken()
    {
        try
        {
            string? authorizationHeader = HttpContext.Request.Headers["Authorization"];
            if (string.IsNullOrEmpty(authorizationHeader))
            {
                Response.StatusCode = (int)HttpStatusCode.BadRequest;
                return new { Message = "Refresh token is required" };
            }

            string refreshToken = authorizationHeader.Replace("Bearer ", "");

            // Validate the token using JwtToken.ValidateToken
            if (!JwtToken.ValidateToken(refreshToken, out Dictionary<string, object>? claims))
            {
                Response.StatusCode = (int)HttpStatusCode.BadRequest;
                return new { Message = "Invalid token" };
            }

            // Generate a new token using the claims
            var newToken = JwtToken.GenerateToken(claims!, TimeSpan.FromHours(ExpirationTimeHours)); // New expiration time for the token

            Response.StatusCode = (int)HttpStatusCode.OK;
            return new { Message = "Token refreshed", Token = newToken };
        }
        catch (Exception ex)
        {
            Response.StatusCode = (int)HttpStatusCode.InternalServerError;
            return new { Message = "An error occurred", Error = ex.Message };
        }
    }
}