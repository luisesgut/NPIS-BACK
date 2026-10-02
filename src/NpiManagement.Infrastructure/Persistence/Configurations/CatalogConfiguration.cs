using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NpiManagement.Infrastructure.Persistence.Entities;

namespace NpiManagement.Infrastructure.Persistence.Configurations;

public sealed class CatalogConfiguration : IEntityTypeConfiguration<Catalog>
{
    public void Configure(EntityTypeBuilder<Catalog> builder)
    {
        builder.ToTable("Catalogs");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.Key).HasMaxLength(50).IsRequired();
        builder.HasIndex(c => c.Key).IsUnique();

        builder.Property(c => c.DisplayName).HasMaxLength(150).IsRequired();

        builder.HasMany(c => c.Options)
            .WithOne(o => o.Catalog)
            .HasForeignKey(o => o.CatalogId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class CatalogOptionConfiguration : IEntityTypeConfiguration<CatalogOption>
{
    public void Configure(EntityTypeBuilder<CatalogOption> builder)
    {
        builder.ToTable("CatalogOptions");
        builder.HasKey(o => o.Id);

        builder.Property(o => o.Value).HasMaxLength(150).IsRequired();

        builder.HasIndex(o => new { o.CatalogId, o.Value }).IsUnique();
    }
}
