using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NpiManagement.Domain.Entities;

namespace NpiManagement.Infrastructure.Persistence.Configurations;

public sealed class InformationRequestConfiguration : IEntityTypeConfiguration<InformationRequest>
{
    public void Configure(EntityTypeBuilder<InformationRequest> builder)
    {
        builder.ToTable("InformationRequests");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).ValueGeneratedNever();

        builder.Property(r => r.Type).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(r => r.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(r => r.Text).HasMaxLength(4000).IsRequired();
        builder.Property(r => r.RequestedByUserId).IsRequired();
        builder.Property(r => r.RequestedByName).HasMaxLength(200).IsRequired();
        builder.Property(r => r.RequestedAt).IsRequired();
        builder.Property(r => r.ResolvedAt);
        builder.Property(r => r.ResolvedByUserId);
        builder.Property(r => r.ResponseText).HasMaxLength(4000);

        builder.Property<Guid>("NpiId").IsRequired();
        builder.HasIndex("NpiId");
        builder.HasIndex(r => r.Status);
    }
}
