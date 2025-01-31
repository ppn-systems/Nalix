using Microsoft.EntityFrameworkCore;
using Notio.Database.Entities;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Notio.Database.Extensions;

public static partial class DbContextExtensions
{
    /// <summary>
    /// Get a user by username.
    /// </summary>
    public static async Task<User> GetByUsernameAsync(this DbSet<User> users, string username)
        => await users.FirstOrDefaultAsync(u => u.Username == username);

    /// <summary>
    /// Get all chats a user is part of.
    /// </summary>
    public static async Task<List<Chat>> GetUserChatsAsync(this DbSet<UserChat> userChats, int userId)
        => await userChats
            .Where(uc => uc.UserId == userId)
            .Select(uc => uc.Chat)
            .ToListAsync();

    /// <summary>
    /// Check if a username is already taken.
    /// </summary>
    public static async Task<bool> UsernameExistsAsync(this DbSet<User> users, string username)
        => await users.AnyAsync(u => u.Username == username);

    /// <summary>
    /// Get user by email.
    /// </summary>
    public static async Task<User> GetByEmailAsync(this DbSet<User> users, string email)
        => await users.FirstOrDefaultAsync(u => u.Email == email);

    /// <summary>
    /// Get all users sorted alphabetically.
    /// </summary>
    public static async Task<List<User>> GetAllSortedAsync(this DbSet<User> users)
        => await users.OrderBy(u => u.Username).ToListAsync();

    /// <summary>
    /// Get user by username or email.
    /// </summary>
    public static async Task<User> GetByUsernameOrEmailAsync(this DbSet<User> users, string input)
        => await users
            .Where(u => u.Username == input || u.Email == input)
            .FirstOrDefaultAsync();
}