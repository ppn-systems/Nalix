using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Notio.Database.Entities;

namespace Notio.Database.Configurations;

public class MessageAttachmentConfiguration : BaseEntityConfiguration<MessageAttachment>
{
    public override void Configure(EntityTypeBuilder<MessageAttachment> builder)
    {
        base.Configure(builder);

        builder.ToTable("MessageAttachments");

        builder.HasKey(e => e.AttachmentId);

        builder.Property(e => e.FileUrl)
            .IsRequired()
            .HasMaxLength(255);

        builder.Property(e => e.FileName)
            .IsRequired()
            .HasMaxLength(255);

        builder.Property(e => e.FileType)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(e => e.FileSize)
            .IsRequired();

        // Index for file type filtering
        builder.HasIndex(e => e.FileType)
            .HasDatabaseName("IX_MessageAttachments_FileType");
    }
}