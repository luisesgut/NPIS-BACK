using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NpiManagement.Infrastructure.Persistence.Entities;

namespace NpiManagement.Infrastructure.Persistence.Configurations;

public sealed class CatalogChangeLogConfiguration : IEntityTypeConfiguration<CatalogChangeLog>
{
    public void Configure(EntityTypeBuilder<CatalogChangeLog> builder)
    {
        builder.ToTable("CatalogChangeLogs");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.CatalogKey).HasMaxLength(50).IsRequired();
        builder.Property(c => c.Action).HasMaxLength(20).IsRequired();
        builder.Property(c => c.OldValue).HasMaxLength(150);
        builder.Property(c => c.NewValue).HasMaxLength(150);
        builder.Property(c => c.ChangedByName).HasMaxLength(200).IsRequired();
        builder.HasIndex(c => new { c.CatalogId, c.ChangedAt });

        // Sin FK a usuarios: el historial debe sobrevivir aunque la cuenta se elimine.
        builder.HasOne<Catalog>()
            .WithMany()
            .HasForeignKey(c => c.CatalogId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
