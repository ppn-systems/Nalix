using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Notio.Database.Entities;
using Notio.Database.Enums;

namespace Notio.Database.Configurations;

public class UserChatConfiguration : BaseEntityConfiguration<UserChat>
{
    public override void Configure(EntityTypeBuilder<UserChat> builder)
    {
        base.Configure(builder);

        builder.ToTable("UserChats");

        builder.HasKey(e => new { e.UserId, e.ChatId });

        builder.Property(e => e.UserRole)
            .HasDefaultValue(UserRole.Member)
            .IsRequired();

        // Indexes
        builder.HasIndex(e => new { e.ChatId, e.UserRole })
            .HasDatabaseName("IX_UserChats_ChatId_UserRole");

        builder.HasIndex(e => e.LastReadAt)
            .HasDatabaseName("IX_UserChats_LastReadAt");
    }
}