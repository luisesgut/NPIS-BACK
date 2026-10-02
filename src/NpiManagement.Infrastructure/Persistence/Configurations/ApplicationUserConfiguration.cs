using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NpiManagement.Infrastructure.Identity;

namespace NpiManagement.Infrastructure.Persistence.Configurations;

public sealed class ApplicationUserConfiguration : IEntityTypeConfiguration<ApplicationUser>
{
    public void Configure(EntityTypeBuilder<ApplicationUser> builder)
    {
        builder.Property(u => u.FullName).HasMaxLength(200).IsRequired();
        builder.Property(u => u.Area).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(u => u.Status).HasConversion<string>().HasMaxLength(20).IsRequired();

        // IsActive se deriva de Status (ver ApplicationUser) — no es columna.
        builder.Ignore(u => u.IsActive);

        builder.HasIndex(u => u.Area);
        builder.HasIndex(u => u.Status);
    }
}
