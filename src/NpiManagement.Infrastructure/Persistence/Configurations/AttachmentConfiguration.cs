using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NpiManagement.Domain.Entities;

namespace NpiManagement.Infrastructure.Persistence.Configurations;

/// <summary>
/// Metadatos de archivo únicamente — el contenido binario nunca se persiste aquí (ADR-021).
/// </summary>
public sealed class AttachmentConfiguration : IEntityTypeConfiguration<Attachment>
{
    public void Configure(EntityTypeBuilder<Attachment> builder)
    {
        builder.ToTable("Attachments");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).ValueGeneratedNever();

        builder.Property(a => a.FileName).HasMaxLength(260).IsRequired();
        builder.Property(a => a.ContentType).HasMaxLength(150).IsRequired();
        builder.Property(a => a.SizeBytes).IsRequired();
        builder.Property(a => a.StorageKey).HasMaxLength(500).IsRequired();
        builder.Property(a => a.Sha256Hash).HasMaxLength(64);
        builder.Property(a => a.Context).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(a => a.UploadedAt).IsRequired();
        builder.Property(a => a.UploadedByUserId).IsRequired();
        builder.Property(a => a.Version).IsRequired();

        builder.Property<Guid>("NpiId").IsRequired();
        builder.HasIndex("NpiId");
        builder.HasIndex(a => a.Context);
        builder.HasIndex(a => a.StorageKey).IsUnique();
    }
}
