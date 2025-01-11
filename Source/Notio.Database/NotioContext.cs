using Microsoft.EntityFrameworkCore;
using Notio.Common.Database;
using Notio.Common.Database.Db;

namespace Notio.Database;

public sealed class NotioContext(DbContextOptions<NotioContext> options) : DbContext(options)
{
    public DbSet<User> Users { get; set; }
    public DbSet<Chat> Chats { get; set; }
    public DbSet<UserChat> UserChats { get; set; }
    public DbSet<Message> Messages { get; set; }
    public DbSet<MessageAttachment> MessageAttachments { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Cấu hình cho User
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.UserId);
            entity.HasIndex(e => e.Username).IsUnique();
            entity.HasIndex(e => e.Email).IsUnique();
            entity.Property(e => e.PasswordHash).IsRequired().HasMaxLength(60);
            entity.Property(e => e.Username).IsRequired().HasMaxLength(50);
            entity.Property(e => e.DisplayName).HasMaxLength(100);
            entity.Property(e => e.Email).HasMaxLength(100);
            entity.Property(e => e.AvatarUrl).HasMaxLength(255);
        });

        // Cấu hình cho Chat
        modelBuilder.Entity<Chat>(entity =>
        {
            entity.HasKey(e => e.ChatId);
            entity.Property(e => e.ChatName).HasMaxLength(100);
            entity.HasIndex(e => e.LastActivityAt);
        });

        // Cấu hình cho UserChat (many-to-many)
        modelBuilder.Entity<UserChat>(entity =>
        {
            entity.HasKey(e => new { e.UserId, e.ChatId });

            entity.HasOne(uc => uc.User)
                .WithMany(u => u.UserChats)
                .HasForeignKey(uc => uc.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(uc => uc.Chat)
                .WithMany(c => c.UserChats)
                .HasForeignKey(uc => uc.ChatId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(e => new { e.ChatId, e.UserRole });
        });

        // Cấu hình cho Data
        modelBuilder.Entity<Message>(entity =>
        {
            entity.HasKey(e => e.MessageId);

            entity.HasOne(m => m.Sender)
                .WithMany(u => u.SentMessages)
                .HasForeignKey(m => m.SenderId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(m => m.Chat)
                .WithMany(c => c.Messages)
                .HasForeignKey(m => m.ChatId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(e => new { e.ChatId, e.CreatedAt });
            entity.HasIndex(e => new { e.SenderId, e.CreatedAt });
        });

        // Cấu hình cho MessageAttachment
        modelBuilder.Entity<MessageAttachment>(entity =>
        {
            entity.HasKey(e => e.AttachmentId);

            entity.HasOne(ma => ma.Message)
                .WithMany(m => m.Attachments)
                .HasForeignKey(ma => ma.MessageId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.Property(e => e.FileUrl).HasMaxLength(255);
            entity.Property(e => e.FileName).HasMaxLength(255);
            entity.Property(e => e.FileType).HasMaxLength(50);
        });
    }
}