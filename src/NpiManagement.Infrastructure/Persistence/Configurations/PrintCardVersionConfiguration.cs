using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NpiManagement.Domain.Entities;

namespace NpiManagement.Infrastructure.Persistence.Configurations;

public sealed class PrintCardVersionConfiguration : IEntityTypeConfiguration<PrintCardVersion>
{
    public void Configure(EntityTypeBuilder<PrintCardVersion> builder)
    {
        builder.ToTable("PrintCardVersions");
        builder.HasKey(v => v.Id);
        builder.Property(v => v.Id).ValueGeneratedNever();

        builder.Property(v => v.VersionNumber).IsRequired();
        builder.Property(v => v.Code).HasMaxLength(50).IsRequired();
        builder.Property(v => v.AttachmentId);
        builder.Property(v => v.UploadedByUserId).IsRequired();
        builder.Property(v => v.UploadedByName).HasMaxLength(200).IsRequired();
        builder.Property(v => v.UploadedAt).IsRequired();
        builder.Property(v => v.Note).HasMaxLength(2000);

        builder.Property<Guid>("NpiId").IsRequired();
        builder.HasIndex("NpiId");
        builder.HasIndex("NpiId", nameof(PrintCardVersion.VersionNumber)).IsUnique();

        builder.HasMany<PrintCardApproval>(nameof(PrintCardVersion.Approvals))
            .WithOne()
            .HasForeignKey("PrintCardVersionId")
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(nameof(PrintCardVersion.Approvals))
            .HasField("_approvals")
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

public sealed class PrintCardApprovalConfiguration : IEntityTypeConfiguration<PrintCardApproval>
{
    public void Configure(EntityTypeBuilder<PrintCardApproval> builder)
    {
        builder.ToTable("PrintCardApprovals");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).ValueGeneratedNever();

        builder.Property(a => a.PrintCardVersionId).IsRequired();
        builder.Property(a => a.Role).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(a => a.ByUserId).IsRequired();
        builder.Property(a => a.ByName).HasMaxLength(200).IsRequired();
        builder.Property(a => a.At).IsRequired();
        builder.Property(a => a.Note).HasMaxLength(2000);
        builder.Property(a => a.EvidenceAttachmentId);

        builder.HasIndex("PrintCardVersionId", nameof(PrintCardApproval.Role)).IsUnique();
    }
}
