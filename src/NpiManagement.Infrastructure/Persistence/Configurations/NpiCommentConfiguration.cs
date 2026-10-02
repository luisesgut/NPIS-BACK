using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NpiManagement.Domain.Entities;

namespace NpiManagement.Infrastructure.Persistence.Configurations;

public sealed class NpiCommentConfiguration : IEntityTypeConfiguration<NpiComment>
{
    public void Configure(EntityTypeBuilder<NpiComment> builder)
    {
        builder.ToTable("NpiComments");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).ValueGeneratedNever();

        builder.Property(c => c.AuthorUserId).IsRequired();
        builder.Property(c => c.AuthorName).HasMaxLength(200).IsRequired();
        builder.Property(c => c.Text).HasMaxLength(4000).IsRequired();
        builder.Property(c => c.CreatedAt).IsRequired();

        builder.Property<Guid>("NpiId").IsRequired();
        builder.HasIndex("NpiId");
        builder.HasIndex(c => c.CreatedAt);
    }
}
