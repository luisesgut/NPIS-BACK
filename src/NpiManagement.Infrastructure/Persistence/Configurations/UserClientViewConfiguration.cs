using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NpiManagement.Application.Users;
using NpiManagement.Infrastructure.Identity;
using NpiManagement.Infrastructure.Persistence.Entities;

namespace NpiManagement.Infrastructure.Persistence.Configurations;

public sealed class UserClientViewConfiguration : IEntityTypeConfiguration<UserClientView>
{
    public void Configure(EntityTypeBuilder<UserClientView> builder)
    {
        builder.ToTable("UserClientViews");
        builder.HasKey(v => new { v.UserId, v.Cliente });
        builder.Property(v => v.Cliente).HasMaxLength(ClientViewGrants.ClienteMaxLength).IsRequired();
        builder.Property(v => v.GrantedAt).IsRequired();
        builder.Property(v => v.CanWork).HasDefaultValue(false);
        // "¿Quién trabaja los NPIs de este cliente?" (tarjeta de equipo del detalle).
        builder.HasIndex(v => v.Cliente);

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(v => v.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
