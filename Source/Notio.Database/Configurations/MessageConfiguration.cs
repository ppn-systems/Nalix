using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Notio.Database.Entities;
using Notio.Database.Enums;

namespace Notio.Database.Configurations;

public class MessageConfiguration : BaseEntityConfiguration<Message>
{
    public override void Configure(EntityTypeBuilder<Message> builder)
    {
        base.Configure(builder);

        builder.ToTable("Messages");

        builder.HasKey(e => e.MessageId);

        builder.Property(e => e.Content)
            .IsRequired();

        builder.Property(e => e.MessageType)
            .HasDefaultValue(MessageType.Text)
            .IsRequired();

        // Indexes for efficient querying
        builder.HasIndex(e => new { e.ChatId, e.CreatedAt })
            .HasDatabaseName("IX_Messages_ChatId_CreatedAt");

        builder.HasIndex(e => new { e.SenderId, e.CreatedAt })
            .HasDatabaseName("IX_Messages_SenderId_CreatedAt");

        builder.HasIndex(e => e.IsDeleted)
            .HasDatabaseName("IX_Messages_IsDeleted");
    }
}