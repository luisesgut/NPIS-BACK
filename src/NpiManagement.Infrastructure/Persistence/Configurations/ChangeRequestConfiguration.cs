using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NpiManagement.Domain.Entities;

namespace NpiManagement.Infrastructure.Persistence.Configurations;

public sealed class ChangeRequestConfiguration : IEntityTypeConfiguration<ChangeRequest>
{
    public void Configure(EntityTypeBuilder<ChangeRequest> builder)
    {
        builder.ToTable("ChangeRequests");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).ValueGeneratedNever();

        builder.Property(c => c.RequestedByUserId).IsRequired();
        builder.Property(c => c.RequestedByName).HasMaxLength(200).IsRequired();
        builder.Property(c => c.RequestedAt).IsRequired();
        builder.Property(c => c.FromArea).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(c => c.ToArea).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(c => c.Origin).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(c => c.Description).HasMaxLength(4000).IsRequired();

        // Lista de claves de campo del propio NPI (catalogadas en npiFields.ts) — se guarda como
        // JSON: es una anotación de conjunto sin ciclo de vida propio (ver assumptions B10).
        builder.Property(c => c.FieldKeys)
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
    }
}
