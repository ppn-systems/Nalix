using Microsoft.EntityFrameworkCore;
using Notio.Database.Entities;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Notio.Database.Extensions;

public static partial class DbContextExtensions
{
    /// <summary>
    /// Get all attachments of a user.
    /// </summary>
    public static async Task<List<MessageAttachment>> GetUserAttachmentsAsync(this DbSet<MessageAttachment> attachments, int userId)
        => await attachments
            .Where(a => a.Message.SenderId == userId)
            .OrderByDescending(a => a.CreatedAt)
            .ToListAsync();

    /// <summary>
    /// Get attachments filtered by file type (e.g., "image/png", "application/pdf").
    /// </summary>
    public static async Task<List<MessageAttachment>> GetAttachmentsByTypeAsync(this DbSet<MessageAttachment> attachments, string fileType)
        => await attachments
            .Where(a => a.FileType == fileType)
            .OrderByDescending(a => a.CreatedAt)
            .ToListAsync();
}