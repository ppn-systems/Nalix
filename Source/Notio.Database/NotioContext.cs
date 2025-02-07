using Microsoft.EntityFrameworkCore;
using Notio.Database.Configurations;
using Notio.Database.Entities;
using System;
using System.IO;
using System.Text.Json;

namespace Notio.Database;

public sealed class NotioContext(DbContextOptions<NotioContext> options) : DbContext(options)
{
    public static readonly string AzureSqlConnection = GetConnectionString("AzureSql");
    public static readonly string LocalDbConnection = GetConnectionString("LocalDb");

    public DbSet<User> Users { get; set; }
    public DbSet<Chat> Chats { get; set; }
    public DbSet<Message> Messages { get; set; }
    public DbSet<UserChat> UserChats { get; set; }
    public DbSet<MessageAttachment> MessageAttachments { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfiguration(new UserConfiguration());
        modelBuilder.ApplyConfiguration(new ChatConfiguration());
        modelBuilder.ApplyConfiguration(new MessageConfiguration());
        modelBuilder.ApplyConfiguration(new UserChatConfiguration());
        modelBuilder.ApplyConfiguration(new MessageAttachmentConfiguration());
    }

    private static string GetConnectionString(string name)
    {
        string configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "appsettings.json");
        if (!File.Exists(configPath)) throw new FileNotFoundException("Config file not found", configPath);

        using var doc = JsonDocument.Parse(File.ReadAllText(configPath));
        return doc.RootElement.GetProperty("ConnectionStrings").GetProperty(name).GetString() ?? string.Empty;
    }

    // Method to create DbContext with the connection string
    public static NotioContext CreateContextWithConnectionString()
    {
        var options = new DbContextOptionsBuilder<NotioContext>()
            .UseSqlServer(AzureSqlConnection, options =>
                options.EnableRetryOnFailure(
                maxRetryCount: 5, // Số lần thử lại tối đa
                maxRetryDelay: TimeSpan.FromSeconds(30), // Thời gian chờ tối đa giữa các lần thử lại
                errorNumbersToAdd: null)
            ).Options;

        // Create and return the context
        return new NotioContext(options);
    }
}