using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NpiManagement.Infrastructure.Persistence.Entities;

namespace NpiManagement.Infrastructure.Persistence.Configurations;

public sealed class QuotationConfiguration : IEntityTypeConfiguration<Quotation>
{
    public void Configure(EntityTypeBuilder<Quotation> builder)
    {
        builder.ToTable("Quotations");
        builder.HasKey(q => q.Id);

        builder.Property(q => q.NumeroCotizacion).HasMaxLength(30).IsRequired();
        builder.HasIndex(q => q.NumeroCotizacion).IsUnique();

        builder.Property(q => q.Cliente).HasMaxLength(200).IsRequired();
        builder.Property(q => q.TipoProducto).HasMaxLength(100).IsRequired();
        builder.Property(q => q.Descripcion).HasMaxLength(500).IsRequired();
        builder.Property(q => q.DescripcionProducto).HasMaxLength(500).IsRequired();
        builder.Property(q => q.EjecutivoVenta).HasMaxLength(200).IsRequired();
        builder.Property(q => q.Ancho).HasMaxLength(50);
        builder.Property(q => q.PiTipoPelicula).HasMaxLength(100);
        builder.Property(q => q.PiCalibre).HasMaxLength(50);
        builder.Property(q => q.PplTipoPelicula).HasMaxLength(100);
        builder.Property(q => q.PplCalibre).HasMaxLength(50);
        builder.Property(q => q.PslTipoPelicula).HasMaxLength(100);
        builder.Property(q => q.PslCalibre).HasMaxLength(50);
        builder.Property(q => q.PesoBobina).HasMaxLength(50);
        builder.Property(q => q.TaponCore).HasMaxLength(10);
        builder.Property(q => q.PiezasCajaBulto).HasMaxLength(50);
    }
}
