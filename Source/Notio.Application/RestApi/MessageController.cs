using Microsoft.EntityFrameworkCore;
using Notio.Database;
using Notio.Database.Entities;
using Notio.Database.Enums;
using Notio.Logging;
using Notio.Network.Web.Enums;
using Notio.Network.Web.Routing;
using Notio.Network.Web.Security;
using Notio.Network.Web.WebApi;
using System;
using System.Linq;
using System.Net;
using System.Threading.Tasks;

namespace Notio.Application.RestApi;

internal class MessageController(NotioContext context) : WebApiController
{
    private readonly NotioContext _context = context;

    public class MessageDto
    {
        public int Id { get; set; }
        public int SenderId { get; set; }
        public string Content { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }

        public MessageType MessageType { get; set; }
    }

    [Route(HttpVerbs.Get, "/messages/{chatId}")]
    public async Task<object> GetMessages(int chatId)
    {
        try
        {
            var user = await GetUserFromToken();
            if (user == null) return new { Message = "Unauthorized" };

            if (!await UserHasAccessToChat(user.UserId, chatId))
            {
                Response.StatusCode = (int)HttpStatusCode.Forbidden;
                return new { Message = "Access denied" };
            }

            var messages = await _context.Messages
                .Where(m => m.ChatId == chatId)  // Filter by ChatId
                .OrderBy(m => m.CreatedAt)  // Order by CreatedAt
                .Select(m => new MessageDto
                {
                    Id = m.MessageId,  // Use MessageId for the Id field
                    SenderId = m.SenderId,
                    Content = m.Content,
                    CreatedAt = m.CreatedAt,
                    MessageType = m.MessageType
                })
                .ToListAsync();

            Response.StatusCode = (int)HttpStatusCode.OK;
            return messages.Count > 0 ? new { Message = messages } : new { Message = "No messages available" };
        }
#if DEBUG
        catch (Exception ex)
#else
        catch (Exception)
#endif
        {
            Response.StatusCode = (int)HttpStatusCode.InternalServerError;
            return new
            {
                Message = "Internal server error",
#if DEBUG
                Error = $"Failed to retrieve messages: {ex.Message}"
#endif
            };
        }
    }

    [Route(HttpVerbs.Get, "/chats")]
    public async Task<object> GetUserChats()
    {
        try
        {
            var user = await GetUserFromToken();
            if (user == null)
            {
                Console.WriteLine("Unauthorized: User not found from token.");
                return new { Message = "Unauthorized" };
            }

            Console.WriteLine($"User found: {user.UserId}");

            var userChats = await _context.UserChats
                .Where(uc => uc.UserId == user.UserId)
                .ToListAsync();
            Console.WriteLine($"UserChats found: {userChats.Count}");

            var chats = await _context.UserChats
                .Where(uc => uc.UserId == user.UserId)
                .Select(uc => new
                {
                    uc.ChatId,
                    uc.Chat.ChatName,
                    LastMessage = _context.Messages
                        .Where(m => m.ChatId == uc.ChatId)
                        .OrderByDescending(m => m.CreatedAt)
                        .Select(m => new
                        {
                            m.Content,
                            m.CreatedAt
                        })
                        .FirstOrDefault()
                })
                .ToListAsync();

            if (chats.Count > 0)
            {
                Console.WriteLine($"Success: Retrieved {chats.Count} chats.");
                return new { Message = "Success", Data = chats };
            }
            else
            {
                Console.WriteLine("No chats available.");
                return new { Message = "No chats available" };
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Internal server error: {ex.Message}");
            Response.StatusCode = (int)HttpStatusCode.InternalServerError;
            return new
            {
                Message = "Internal server error",
#if DEBUG
                Error = $"Failed to retrieve chats: {ex.Message}"
#endif
            };
        }
    }

    // Extracted method to get the token from the request headers
    private string? GetTokenFromRequest()
    {
        return HttpContext.Request.Headers["Authorization"]?.Replace("Bearer ", "");
    }

    private async Task<User?> GetUserFromToken()
    {
        try
        {
            var token = GetTokenFromRequest();
            if (string.IsNullOrEmpty(token))
            {
                Response.StatusCode = (int)HttpStatusCode.Unauthorized;
                return null;
            }

            var claims = JwtToken.DecodeToken(token);
            if (!claims.TryGetValue("sub", out object? userIdObj) || userIdObj == null)
            {
                Response.StatusCode = (int)HttpStatusCode.Unauthorized;
                return null;
            }

            if (!int.TryParse(userIdObj.ToString(), out int userId))
            {
                Response.StatusCode = (int)HttpStatusCode.BadRequest;
                return null;
            }

            return await _context.Users.SingleOrDefaultAsync(u => u.UserId == userId);
        }
        catch (Exception ex)
        {
            // Log the exception for debugging
            NotioLog.Instance.Error("Error getting user from token", ex);
            Response.StatusCode = (int)HttpStatusCode.Unauthorized;
            return null;
        }
    }

    private async Task<bool> UserHasAccessToChat(int userId, int chatId)
    {
        try
        {
            return await _context.UserChats
                .AnyAsync(uc => uc.ChatId == chatId && uc.UserId == userId);
        }
        catch (Exception ex)
        {
            NotioLog.Instance.Error("Error checking user access to chat", ex);
            return false;
        }
    }
}