using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NpiManagement.Application.Abstractions;
using NpiManagement.Infrastructure.Identity;
using NpiManagement.Infrastructure.Persistence.Entities;

namespace NpiManagement.Infrastructure.Persistence.Configurations;

public sealed class FeedbackReportConfiguration : IEntityTypeConfiguration<FeedbackReport>
{
    public void Configure(EntityTypeBuilder<FeedbackReport> builder)
    {
        builder.ToTable("FeedbackReports");
        builder.HasKey(f => f.Id);

        builder.Property(f => f.ReporterName).HasMaxLength(200).IsRequired();
        builder.Property(f => f.ReporterRole).HasMaxLength(50).IsRequired();
        builder.Property(f => f.Kind).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(f => f.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(f => f.Description).HasMaxLength(FeedbackLimits.DescriptionMaxLength).IsRequired();
        builder.Property(f => f.PageUrl).HasMaxLength(FeedbackLimits.PageUrlMaxLength);
        builder.Property(f => f.UserAgent).HasMaxLength(FeedbackLimits.UserAgentMaxLength);
        builder.Property(f => f.ScreenSize).HasMaxLength(FeedbackLimits.ScreenSizeMaxLength);
        builder.Property(f => f.TechnicalDetails).HasMaxLength(FeedbackLimits.TechnicalDetailsMaxLength);
        builder.Property(f => f.ScreenshotStorageKey).HasMaxLength(200);
        builder.Property(f => f.ScreenshotContentType).HasMaxLength(100);
        builder.Property(f => f.AdminResponse).HasMaxLength(FeedbackLimits.AdminResponseMaxLength);
        builder.Property(f => f.CreatedAt).IsRequired();

        builder.HasIndex(f => new { f.Status, f.CreatedAt });
        builder.HasIndex(f => f.ReporterUserId);

        // Restrict y no Cascade: las cuentas se dan de baja, no se borran. Si algún día se borrara
        // una, perder en silencio los reportes que hizo sería perder justo la evidencia de un problema.
        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(f => f.ReporterUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
