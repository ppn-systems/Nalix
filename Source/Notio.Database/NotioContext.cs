using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Notio.Database.Entities;
using System;
using System.IO;

namespace Notio.Database;

public sealed class NotioContext(DbContextOptions<NotioContext> options) : DbContext(options)
{
    public static readonly string AzureSqlConnection = GetConnectionString();

    public static readonly string LocalDbConnection = $"Data Source={Path.Combine(Directory.GetCurrentDirectory(), "notio.db")}";
    public DbSet<User> Users { get; set; }
    public DbSet<Chat> Chats { get; set; }
    public DbSet<Message> Messages { get; set; }
    public DbSet<UserChat> UserChats { get; set; }
    public DbSet<MessageAttachment> MessageAttachments { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ModelConfiguration.UserEntity(modelBuilder);
        ModelConfiguration.ChatEntity(modelBuilder);
        ModelConfiguration.MessageEntity(modelBuilder);
        ModelConfiguration.UserChatEntity(modelBuilder);
        ModelConfiguration.MessageAttachmentEntity(modelBuilder);
    }

    private static string GetConnectionString()
    {
        // Xây dựng cấu hình từ file appsettings.json
        var configuration = new ConfigurationBuilder()
            .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .Build();

        // Lấy connection string từ file cấu hình
        string connectionString = configuration
            .GetSection("ConnectionStrings")
            .GetSection("AzureSql").Value;

        return connectionString;
    }
}