using Microsoft.EntityFrameworkCore;
using Notio.Database;
using Notio.Database.Entities;
using Notio.Web.Enums;
using Notio.Web.Http;
using Notio.Web.Routing;
using Notio.Web.WebApi;
using System;
using System.Linq;
using System.Net;
using System.Threading.Tasks;

namespace Notio.Application.RestApi;

internal class UserController(NotioContext context) : WebApiController
{
    private readonly NotioContext _context = context;

    [Route(HttpVerbs.Get, "/users")]
    public async Task<object> GetUsers()
    {
        var users = await _context.Users
            .Select(u => new
            {
                u.UserId,
                u.Username,
                u.DisplayName,
                u.Email,
                u.AvatarUrl
            })
            .ToListAsync();

        Response.StatusCode = (int)HttpStatusCode.OK;
        return users;
    }

    [Route(HttpVerbs.Get, "/users/{id}")]
    public async Task<object> GetUserById(int id)
    {
        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.UserId == id);

        if (user == null)
        {
            Response.StatusCode = (int)HttpStatusCode.NotFound;
            return new { Message = "User not found" };
        }

        Response.StatusCode = (int)HttpStatusCode.OK;
        return new
        {
            user.UserId,
            user.Username,
            user.DisplayName,
            user.Email,
            user.AvatarUrl
        };
    }

    [Route(HttpVerbs.Post, "/users")]
    public async Task<object> CreateUser()
    {
        try
        {
            // Đọc dữ liệu từ Request body
            User userData = await HttpContext.GetRequestDataAsync<User>();

            // Kiểm tra username và email có tồn tại chưa
            if (await _context.Users.AnyAsync(u => u.Username == userData.Username))
            {
                Response.StatusCode = (int)HttpStatusCode.BadRequest;
                return new { message = "Username đã tồn tại" };
            }

            if (await _context.Users.AnyAsync(u => u.Email == userData.Email))
            {
                Response.StatusCode = (int)HttpStatusCode.BadRequest;
                return new { message = "Email đã tồn tại" };
            }

            await _context.Users.AddAsync(userData);
            await _context.SaveChangesAsync();

            Response.StatusCode = (int)HttpStatusCode.Created;
            return userData;
        }
        catch (Exception ex)
        {
            Response.StatusCode = (int)HttpStatusCode.InternalServerError;
            return new { message = "Lỗi khi tạo người dùng", error = ex.Message };
        }
    }
}