using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NpiManagement.Domain;
using NpiManagement.Domain.Entities;
using NpiManagement.Infrastructure.Identity;
using NpiManagement.Infrastructure.Persistence.Entities;

namespace NpiManagement.Infrastructure.Persistence.Configurations;

/// <summary>
/// Mensajes del chat. Misma tabla que los antiguos comentarios (se conserva el historial), pero ya
/// no como colección del agregado Npi: NpiId pasa de propiedad sombra a propiedad real, con la misma
/// columna, FK y borrado en cascada.
/// </summary>
public sealed class NpiCommentConfiguration : IEntityTypeConfiguration<NpiComment>
{
    public void Configure(EntityTypeBuilder<NpiComment> builder)
    {
        builder.ToTable("NpiComments");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).ValueGeneratedNever();

        builder.Property(c => c.AuthorUserId).IsRequired();
        builder.Property(c => c.AuthorName).HasMaxLength(200).IsRequired();
        builder.Property(c => c.Text).HasMaxLength(NpiComment.MaxTextLength).IsRequired();
        builder.Property(c => c.CreatedAt).IsRequired();

        // Lista corta de Guids guardada como JSON: sólo se usa para mostrar y resaltar la mención,
        // nunca se filtra por ella en SQL (los pendientes salen de NpiChatReads).
        builder.PrimitiveCollection(c => c.MentionedUserIds).IsRequired();

        builder.HasOne<Npi>()
            .WithMany()
            .HasForeignKey(c => c.NpiId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(c => c.NpiId);
        builder.HasIndex(c => c.CreatedAt);
    }
}

/// <summary>Marca "leído hasta" del chat, una fila por NPI y usuario.</summary>
public sealed class NpiChatReadConfiguration : IEntityTypeConfiguration<NpiChatRead>
{
    public void Configure(EntityTypeBuilder<NpiChatRead> builder)
    {
        builder.ToTable("NpiChatReads");
        builder.HasKey(r => new { r.NpiId, r.UserId });
        builder.Property(r => r.LastReadAt).IsRequired();
        builder.HasIndex(r => r.UserId);

        builder.HasOne<Npi>()
            .WithMany()
            .HasForeignKey(r => r.NpiId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(r => r.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
