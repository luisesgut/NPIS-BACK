using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NpiManagement.Domain.Entities;

namespace NpiManagement.Infrastructure.Persistence.Configurations;

public sealed class SisproQuoteLinkConfiguration : IEntityTypeConfiguration<SisproQuoteLink>
{
    public void Configure(EntityTypeBuilder<SisproQuoteLink> builder)
    {
        builder.ToTable("SisproQuoteLinks");
        builder.HasKey(q => q.Id);
        builder.Property(q => q.Id).ValueGeneratedNever();

        builder.Property(q => q.VersionNumber).IsRequired();
        builder.Property(q => q.NumeroCotizacion).HasMaxLength(30).IsRequired();
        builder.Property(q => q.EstadoSispro).HasMaxLength(50).IsRequired();
        // Foto de lo que dijo SISPRO al ligarla (cantidades, precios, capas): se muestra sin volver a consultar.
        builder.Property(q => q.SnapshotJson).IsRequired();
        builder.Property(q => q.LinkedByUserId).IsRequired();
        builder.Property(q => q.LinkedByName).HasMaxLength(200).IsRequired();
        builder.Property(q => q.LinkedAt).IsRequired();
        builder.Property(q => q.Status).HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(q => q.RespondedByName).HasMaxLength(200);
        builder.Property(q => q.ResponseNote).HasMaxLength(2000);
        builder.Property(q => q.InvalidationReason).HasMaxLength(1000);

        // Una diferencia por renglón; puede llevar comas, por eso JSON y no el CSV de FieldKeys.
        builder.Property(q => q.StructureWarnings)
            .HasConversion(
                v => System.Text.Json.JsonSerializer.Serialize(v, (System.Text.Json.JsonSerializerOptions?)null),
                v => System.Text.Json.JsonSerializer.Deserialize<List<string>>(v, (System.Text.Json.JsonSerializerOptions?)null) ?? new List<string>())
            .HasMaxLength(4000)
            .Metadata.SetValueComparer(new ValueComparer<IReadOnlyList<string>>(
                (a, b) => a!.SequenceEqual(b!),
                a => a.Aggregate(0, (hash, s) => HashCode.Combine(hash, s.GetHashCode())),
                a => a.ToList()));

        builder.Property<Guid>("NpiId").IsRequired();
        builder.HasIndex("NpiId");
        builder.HasIndex("NpiId", nameof(SisproQuoteLink.VersionNumber)).IsUnique();
        builder.HasIndex(q => q.NumeroCotizacion);
    }
}
