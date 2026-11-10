using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NpiManagement.Application.Abstractions;
using NpiManagement.Infrastructure.Persistence.Entities;

namespace NpiManagement.Infrastructure.Persistence.Configurations;

public sealed class FeedbackMessageConfiguration : IEntityTypeConfiguration<FeedbackMessage>
{
    public void Configure(EntityTypeBuilder<FeedbackMessage> builder)
    {
        builder.ToTable("FeedbackMessages");
        builder.HasKey(m => m.Id);

        builder.Property(m => m.AuthorName).HasMaxLength(200).IsRequired();
        builder.Property(m => m.Text).HasMaxLength(FeedbackLimits.MessageMaxLength).IsRequired();
        builder.Property(m => m.CreatedAt).IsRequired();

        builder.HasIndex(m => new { m.ReportId, m.CreatedAt });

        // El hilo es parte del reporte: si algún día se borrara el reporte, sus mensajes se van con él.
        builder.HasOne<FeedbackReport>()
            .WithMany()
            .HasForeignKey(m => m.ReportId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
