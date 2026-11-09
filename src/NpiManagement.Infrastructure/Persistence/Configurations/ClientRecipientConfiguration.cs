using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NpiManagement.Infrastructure.Identity;
using NpiManagement.Infrastructure.Persistence.Entities;

namespace NpiManagement.Infrastructure.Persistence.Configurations;

public sealed class ClientRecipientConfiguration : IEntityTypeConfiguration<ClientRecipient>
{
    public void Configure(EntityTypeBuilder<ClientRecipient> builder)
    {
        builder.ToTable("ClientRecipients");
        builder.HasKey(r => r.Id);

        builder.Property(r => r.Cliente).HasMaxLength(200).IsRequired();
        builder.Property(r => r.Email).HasMaxLength(254).IsRequired();
        builder.Property(r => r.Kind).HasMaxLength(2).IsRequired();

        // Un correo una sola vez en la libreta de un vendedor para un cliente.
        builder.HasIndex(r => new { r.UserId, r.Cliente, r.Email }).IsUnique();

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(r => r.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
