using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NpiManagement.Domain.Entities;

namespace NpiManagement.Infrastructure.Persistence.Configurations;

public sealed class NpiParticipantConfiguration : IEntityTypeConfiguration<NpiParticipant>
{
    public void Configure(EntityTypeBuilder<NpiParticipant> builder)
    {
        builder.ToTable("NpiParticipants");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).ValueGeneratedNever();

        builder.Property(p => p.UserId).IsRequired();
        builder.Property(p => p.Area).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(p => p.AddedByUserId).IsRequired();
        builder.Property(p => p.AddedByName).HasMaxLength(200).IsRequired();
        builder.Property(p => p.AddedAt).IsRequired();

        builder.Property<Guid>("NpiId").IsRequired();
        builder.HasIndex("NpiId", nameof(NpiParticipant.UserId), nameof(NpiParticipant.Area)).IsUnique();
        // Listado y "Tu turno" buscan los NPIs donde participa alguien.
        builder.HasIndex(p => p.UserId);
    }
}
