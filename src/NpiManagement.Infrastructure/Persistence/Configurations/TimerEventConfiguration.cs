using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NpiManagement.Domain.Entities;

namespace NpiManagement.Infrastructure.Persistence.Configurations;

/// <summary>Bitácora append-only de cronómetros — nunca se actualiza ni se borra desde la aplicación.</summary>
public sealed class TimerEventConfiguration : IEntityTypeConfiguration<TimerEvent>
{
    public void Configure(EntityTypeBuilder<TimerEvent> builder)
    {
        builder.ToTable("TimerEvents");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedNever();

        builder.Property(e => e.Stage).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(e => e.Action).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(e => e.FromStage).HasConversion<string>().HasMaxLength(20);
        builder.Property(e => e.ToStage).HasConversion<string>().HasMaxLength(20);
        builder.Property(e => e.OccurredAt).IsRequired();
        builder.Property(e => e.ActorUserId);
        builder.Property(e => e.DurationMsAtEvent);
        builder.Property(e => e.Note).HasMaxLength(1000);

        builder.Property<Guid>("NpiId").IsRequired();
        builder.HasIndex("NpiId");
        builder.HasIndex(e => e.OccurredAt);
    }
}
