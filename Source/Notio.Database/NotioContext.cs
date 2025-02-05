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
    public static DbContext CreateContextWithConnectionString()
    {
        var options = new DbContextOptionsBuilder<NotioContext>()
            .UseSqlServer(AzureSqlConnection) // Or use AzureSqlConnection based on the environment
            .Options;

        // Create and return the context
        return new NotioContext(options);
    }
}