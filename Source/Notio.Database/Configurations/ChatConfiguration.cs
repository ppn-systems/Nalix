using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Notio.Database.Entities;

namespace Notio.Database.Configurations;

public class ChatConfiguration : BaseEntityConfiguration<Chat>
{
    public override void Configure(EntityTypeBuilder<Chat> builder)
    {
        base.Configure(builder);

        builder.ToTable("Chats");

        builder.HasKey(e => e.ChatId);

        builder.Property(e => e.ChatName)
            .HasMaxLength(100);

        builder.Property(e => e.LastActivityAt)
            .HasDefaultValueSql("CURRENT_TIMESTAMP")
            .IsRequired();

        // Index
        builder.HasIndex(e => e.LastActivityAt)
            .HasDatabaseName("IX_Chats_LastActivityAt");

        // Relationships
        builder.HasMany(c => c.UserChats)
            .WithOne(uc => uc.Chat)
            .HasForeignKey(uc => uc.ChatId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(c => c.Messages)
            .WithOne(m => m.Chat)
            .HasForeignKey(m => m.ChatId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}