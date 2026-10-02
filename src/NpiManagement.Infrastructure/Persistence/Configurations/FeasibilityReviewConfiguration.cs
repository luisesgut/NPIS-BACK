using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NpiManagement.Domain.Entities;

namespace NpiManagement.Infrastructure.Persistence.Configurations;

public sealed class FeasibilityReviewConfiguration : IEntityTypeConfiguration<FeasibilityReview>
{
    public void Configure(EntityTypeBuilder<FeasibilityReview> builder)
    {
        builder.ToTable("FeasibilityReviews");
        builder.HasKey(f => f.Id);
        builder.Property(f => f.Id).ValueGeneratedNever();

        builder.Property(f => f.Area).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(f => f.ApprovedByUserId).IsRequired();
        builder.Property(f => f.ApprovedByName).HasMaxLength(200).IsRequired();
        builder.Property(f => f.ApprovedAt).IsRequired();
        builder.Property(f => f.Note).HasMaxLength(2000);

        // Mismo patrón que ChangeRequest.FieldKeys (ChangeRequestConfiguration): lista de claves
        // de campo (npiFields.ts) guardada como CSV — es una anotación de conjunto sin ciclo de
        // vida propio, no necesita su propia tabla.
        builder.Property(f => f.ConfirmedFieldKeys)
            .HasConversion(
                v => string.Join(',', v),
                v => v.Length == 0 ? new List<string>() : v.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList())
            .HasMaxLength(2000)
            .Metadata.SetValueComparer(new Microsoft.EntityFrameworkCore.ChangeTracking.ValueComparer<IReadOnlyList<string>>(
                (a, b) => a!.SequenceEqual(b!),
                a => a.Aggregate(0, (hash, s) => HashCode.Combine(hash, s.GetHashCode())),
                a => a.ToList()));

        builder.Property<Guid>("NpiId").IsRequired();
        builder.HasIndex("NpiId");
        // A lo sumo una factibilidad por área por NPI (invariante de dominio, reforzada aquí).
        builder.HasIndex("NpiId", nameof(FeasibilityReview.Area)).IsUnique();
    }
}
