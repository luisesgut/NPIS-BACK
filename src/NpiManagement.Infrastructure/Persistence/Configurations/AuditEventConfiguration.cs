using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NpiManagement.Domain.Entities;

namespace NpiManagement.Infrastructure.Persistence.Configurations;

/// <summary>Bitácora append-only de auditoría de negocio — nunca se actualiza ni se borra desde la aplicación.</summary>
public sealed class AuditEventConfiguration : IEntityTypeConfiguration<AuditEvent>
{
    public void Configure(EntityTypeBuilder<AuditEvent> builder)
    {
        builder.ToTable("AuditEvents");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedNever();

        builder.Property(e => e.ActorUserId).IsRequired();
        builder.Property(e => e.ActorRole).HasMaxLength(50).IsRequired();
        builder.Property(e => e.OccurredAt).IsRequired();
        builder.Property(e => e.Action).HasMaxLength(100).IsRequired();
        builder.Property(e => e.Details).HasMaxLength(4000);

        builder.Property<Guid>("NpiId").IsRequired();
        builder.HasIndex("NpiId");
        builder.HasIndex(e => e.OccurredAt);
        builder.HasIndex(e => e.ActorUserId);
    }
}
