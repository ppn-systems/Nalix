using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Notio.Database.Entities;
using System.Diagnostics.CodeAnalysis;

namespace Notio.Database.Configurations;

public abstract class BaseEntityConfiguration<
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors |
    DynamicallyAccessedMemberTypes.NonPublicConstructors |
    DynamicallyAccessedMemberTypes.PublicFields |
    DynamicallyAccessedMemberTypes.NonPublicFields |
    DynamicallyAccessedMemberTypes.PublicProperties |
    DynamicallyAccessedMemberTypes.NonPublicProperties |
    DynamicallyAccessedMemberTypes.Interfaces)] T> : IEntityTypeConfiguration<T> where T : BaseEntity
{
    public virtual void Configure(EntityTypeBuilder<T> builder)
    {
        // Common configuration cho tất cả entities
        builder.Property(e => e.CreatedAt)
            .HasDefaultValueSql("CURRENT_TIMESTAMP")
            .IsRequired();

        builder.Property(e => e.UpdatedAt)
            .HasDefaultValueSql("CURRENT_TIMESTAMP")
            .ValueGeneratedOnUpdate();
    }
}