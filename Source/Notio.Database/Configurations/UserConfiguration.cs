using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Notio.Database.Entities;

namespace Notio.Database.Configurations;

public class UserConfiguration : BaseEntityConfiguration<User>
{
    public override void Configure(EntityTypeBuilder<User> builder)
    {
        base.Configure(builder);

        builder.ToTable("Users");

        builder.HasKey(e => e.UserId);

        builder.Property(e => e.Username)
            .IsRequired()
            .HasMaxLength(50)
            .UseCollation("SQL_Latin1_General_CP1_CI_AS");

        builder.Property(e => e.PasswordHash)
            .IsRequired()
            .HasMaxLength(200)
            .IsFixedLength();

        builder.Property(e => e.Email)
            .HasMaxLength(100)
            .UseCollation("SQL_Latin1_General_CP1_CI_AS");

        builder.Property(e => e.DisplayName)
            .HasMaxLength(100);

        builder.Property(e => e.AvatarUrl)
            .HasMaxLength(255);

        // Indexes
        builder.HasIndex(e => e.Username)
            .IsUnique()
            .HasFilter("[Username] IS NOT NULL")
            .HasDatabaseName("IX_Users_Username");

        builder.HasIndex(e => e.Email)
            .IsUnique()
            .HasFilter("[Email] IS NOT NULL")
            .HasDatabaseName("IX_Users_Email");
    }
}