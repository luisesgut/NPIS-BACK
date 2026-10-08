using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NpiManagement.Domain;
using NpiManagement.Domain.Entities;

namespace NpiManagement.Infrastructure.Persistence.Configurations;

/// <summary>Bitácora de cambios de la captura, fuera del agregado Npi (ver CaptureChange).</summary>
public sealed class CaptureChangeConfiguration : IEntityTypeConfiguration<CaptureChange>
{
    public void Configure(EntityTypeBuilder<CaptureChange> builder)
    {
        builder.ToTable("NpiCaptureChanges");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).ValueGeneratedNever();

        builder.Property(c => c.Field).HasMaxLength(100).IsRequired();
        builder.Property(c => c.Before).HasMaxLength(2000).IsRequired();
        builder.Property(c => c.After).HasMaxLength(2000).IsRequired();
        builder.Property(c => c.ChangedByName).HasMaxLength(200).IsRequired();
        builder.Property(c => c.ChangedAt).IsRequired();
        // Propiedades de sólo lectura: EF sólo las mapea si se declaran.
        builder.Property(c => c.ChangedByUserId).IsRequired();
        builder.Property(c => c.ChangeRequestId);
        builder.Property(c => c.WasRequested).IsRequired();

        builder.HasOne<Npi>()
            .WithMany()
            .HasForeignKey(c => c.NpiId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(c => new { c.NpiId, c.ChangedAt });
    }
}
