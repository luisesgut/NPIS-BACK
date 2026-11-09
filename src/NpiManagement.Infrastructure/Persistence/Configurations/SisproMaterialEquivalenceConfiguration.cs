using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NpiManagement.Infrastructure.Persistence.Entities;

namespace NpiManagement.Infrastructure.Persistence.Configurations;

public sealed class SisproMaterialEquivalenceConfiguration : IEntityTypeConfiguration<SisproMaterialEquivalence>
{
    public void Configure(EntityTypeBuilder<SisproMaterialEquivalence> builder)
    {
        builder.ToTable("SisproMaterialEquivalences");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedNever();

        builder.Property(e => e.MaterialSispro).HasMaxLength(150).IsRequired();
        builder.HasIndex(e => e.MaterialSispro).IsUnique();
        builder.Property(e => e.TipoPelicula).HasMaxLength(150).IsRequired();
        builder.Property(e => e.Terminado).HasMaxLength(150);
        builder.Property(e => e.FormulaLdpe).HasMaxLength(150);
        builder.Property(e => e.UpdatedAt).IsRequired();
        builder.Property(e => e.UpdatedByName).HasMaxLength(200).IsRequired();
    }
}
