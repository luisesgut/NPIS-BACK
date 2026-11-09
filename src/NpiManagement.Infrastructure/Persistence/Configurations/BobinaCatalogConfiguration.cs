using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NpiManagement.Domain;
using NpiManagement.Infrastructure.Persistence.Entities;

namespace NpiManagement.Infrastructure.Persistence.Configurations;

public sealed class MaterialBobinaConfiguration : IEntityTypeConfiguration<MaterialBobina>
{
    public void Configure(EntityTypeBuilder<MaterialBobina> builder)
    {
        builder.ToTable("MaterialesBobina");
        builder.HasKey(m => m.Id);
        builder.Property(m => m.Codigo).HasMaxLength(60).IsRequired();
        builder.HasIndex(m => m.Codigo).IsUnique();
        builder.Property(m => m.DensidadKgM3).HasPrecision(12, 4);
        builder.Property(m => m.TipoPelicula).HasMaxLength(150);
        builder.Property(m => m.Terminado).HasMaxLength(150);
        builder.Property(m => m.FormulaLdpe).HasMaxLength(150);
        builder.Property(m => m.UpdatedByName).HasMaxLength(200);
    }
}

public sealed class CoreBobinaConfiguration : IEntityTypeConfiguration<CoreBobina>
{
    public void Configure(EntityTypeBuilder<CoreBobina> builder)
    {
        builder.ToTable("CoresBobina");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Nombre).HasMaxLength(150).IsRequired();
        builder.HasIndex(c => c.Nombre).IsUnique();
        builder.Property(c => c.DiametroExteriorM).HasPrecision(10, 5);
        builder.Property(c => c.KgPorCm).HasPrecision(10, 5);
    }
}

public sealed class ParametrosBobinaConfiguration : IEntityTypeConfiguration<ParametrosBobina>
{
    public void Configure(EntityTypeBuilder<ParametrosBobina> builder)
    {
        builder.ToTable("ParametrosBobina");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).ValueGeneratedNever();
        builder.Property(p => p.AdhesivoGm2).HasPrecision(10, 4);
        builder.Property(p => p.TintaGm2).HasPrecision(10, 4);
        builder.Property(p => p.TarimaAnchoCm).HasPrecision(10, 2);
        builder.Property(p => p.TarimaLargoCm).HasPrecision(10, 2);
        builder.Property(p => p.TarimaAltoMaxCm).HasPrecision(10, 2);
        builder.Property(p => p.ToleranciaAlto).HasPrecision(6, 4);
    }
}

public sealed class NpiMaterialExactoConfiguration : IEntityTypeConfiguration<NpiMaterialExacto>
{
    public void Configure(EntityTypeBuilder<NpiMaterialExacto> builder)
    {
        builder.ToTable("NpiMaterialesExactos");
        builder.HasKey(e => new { e.NpiId, e.Capa });
        builder.Property(e => e.Capa).HasMaxLength(10);
        builder.Property(e => e.SetByName).HasMaxLength(200).IsRequired();

        builder.HasOne<Npi>().WithMany().HasForeignKey(e => e.NpiId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(e => e.Material).WithMany().HasForeignKey(e => e.MaterialBobinaId).OnDelete(DeleteBehavior.Restrict);
    }
}
