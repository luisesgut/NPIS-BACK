using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NpiManagement.Domain.Entities;

namespace NpiManagement.Infrastructure.Persistence.Configurations;

public sealed class ClosureProposalConfiguration : IEntityTypeConfiguration<ClosureProposal>
{
    public void Configure(EntityTypeBuilder<ClosureProposal> builder)
    {
        builder.ToTable("NpiClosureProposals");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).ValueGeneratedNever();

        builder.Property(p => p.Reason).HasConversion<string>().HasMaxLength(40).IsRequired();
        builder.Property(p => p.Area).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(p => p.ProposedByUserId).IsRequired();
        builder.Property(p => p.ProposedByName).HasMaxLength(200).IsRequired();
        builder.Property(p => p.ProposedAt).IsRequired();
        builder.Property(p => p.Note).HasMaxLength(2000).IsRequired();
        builder.Property(p => p.EvidenceAttachmentId);
        builder.Property(p => p.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(p => p.DecidedByName).HasMaxLength(200);
        builder.Property(p => p.DecisionNote).HasMaxLength(2000);

        builder.Property<Guid>("NpiId").IsRequired();
        builder.HasIndex("NpiId");
        // Los paneles buscan las pendientes de visto bueno.
        builder.HasIndex(p => p.Status);
    }
}
