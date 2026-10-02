using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NpiManagement.Domain;
using NpiManagement.Infrastructure.Identity;
using NpiManagement.Infrastructure.Persistence.Entities;

namespace NpiManagement.Infrastructure.Persistence.Configurations;

public sealed class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder.ToTable("Notifications");
        builder.HasKey(n => n.Id);

        builder.Property(n => n.Message).HasMaxLength(500).IsRequired();
        builder.Property(n => n.CreatedAt).IsRequired();

        builder.HasIndex(n => new { n.RecipientUserId, n.ReadAt });

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(n => n.RecipientUserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<Npi>()
            .WithMany()
            .HasForeignKey(n => n.NpiId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
