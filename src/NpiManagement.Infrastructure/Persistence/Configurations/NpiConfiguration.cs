using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NpiManagement.Domain;
using NpiManagement.Domain.Entities;
using NpiManagement.Domain.ValueObjects;
using NpiManagement.Infrastructure.Identity;

namespace NpiManagement.Infrastructure.Persistence.Configurations;

/// <summary>
/// Mapeo del agregado raíz Npi: columnas propias, cronómetros embebidos (owned, sin tabla
/// propia), snapshot de captura (owned, ~68 columnas) e incidentes opcionales (owned), más las
/// relaciones uno-a-muchos hacia las entidades satélite (cada una con su propia configuración
/// en este mismo namespace). Ver docs/domain-model.md §2.
/// </summary>
public sealed class NpiConfiguration : IEntityTypeConfiguration<Npi>
{
    public void Configure(EntityTypeBuilder<Npi> builder)
    {
        builder.ToTable("Npis");
        builder.HasKey(n => n.Id);
        builder.Property(n => n.Id).ValueGeneratedNever();

        // Concurrencia optimista (ADR-004): shadow property respaldada por rowversion nativo de
        // SQL Server. No existe una propiedad "RowVersion" en el dominio — es un detalle de
        // persistencia, invisible para Domain.
        builder.Property<byte[]>("RowVersion").IsRowVersion();

        // NpiNumber: value object de un solo string, convertido de/hacia la columna.
        builder.Property(n => n.NpiNumber)
            .HasConversion(
                v => v == null ? null : v.Value,
                v => v == null ? null : NpiNumber.FromExisting(v))
            .HasColumnName("NpiNumber")
            .HasMaxLength(20);

        // Único cuando no es null (los borradores no tienen número todavía; SQL Server sólo
        // permite un NULL por índice único normal, de ahí el índice filtrado).
        builder.HasIndex(n => n.NpiNumber)
            .IsUnique()
            .HasFilter("[NpiNumber] IS NOT NULL")
            .HasDatabaseName("IX_Npis_NpiNumber");

        builder.Property(n => n.Priority).HasConversion<string>().HasMaxLength(10).IsRequired();
        builder.Property(n => n.LifecycleStatus).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(n => n.TerminalReason).HasConversion<string>().HasMaxLength(30);
        builder.Property(n => n.EngineeringAreaStatus).HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(n => n.DesignAreaStatus).HasConversion<string>().HasMaxLength(30).IsRequired();

        builder.Property(n => n.CreatedAt).IsRequired();
        // Foto de la captura al primer envío (JSON) — "fase inicial" de la bitácora de cambios.
        builder.Property(n => n.InitialCaptureJson);
        builder.Property(n => n.RequestDate).IsRequired();

        // Asignaciones y creador: FK a AspNetUsers, sin cascada (borrar un usuario no debe
        // arrastrar los NPIs que creó o tiene asignados).
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(n => n.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(n => n.AssignedCommercialUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(n => n.AssignedEngineeringUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(n => n.AssignedDesignUserId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(n => n.CreatedByUserId);
        builder.HasIndex(n => n.AssignedCommercialUserId);
        builder.HasIndex(n => n.AssignedEngineeringUserId);
        builder.HasIndex(n => n.AssignedDesignUserId);
        builder.HasIndex(n => n.LifecycleStatus);
        builder.HasIndex(n => n.EngineeringAreaStatus);
        builder.HasIndex(n => n.DesignAreaStatus);
        builder.HasIndex(n => n.CreatedAt);
        builder.HasIndex(n => n.RequestDate);

        ConfigureTimer(builder, n => n.GlobalTimer, "GlobalTimer");
        ConfigureTimer(builder, n => n.CommercialTimer, "CommercialTimer");
        ConfigureTimer(builder, n => n.EngineeringTimer, "EngineeringTimer");
        ConfigureTimer(builder, n => n.DesignTimer, "DesignTimer");

        builder.Property(n => n.ArtesValidadasAt);
        builder.Property(n => n.ArtesValidadasByUserId);

        builder.Property(n => n.PrintCardLoadedAt);
        builder.Property(n => n.PrintCardLoadedByUserId);
        builder.Property(n => n.PrintCardLoadedVersionId);
        builder.Property(n => n.PrintCardSentToClientAt);
        builder.Property(n => n.PrintCardSentToClientByUserId);

        builder.Property(n => n.CodigoPT).HasMaxLength(100);
        builder.Property(n => n.PtName).HasMaxLength(200);
        builder.Property(n => n.PtRegisteredAt);
        builder.Property(n => n.PtRegisteredByUserId);

        builder.Property(n => n.PedidoSapNumber).HasMaxLength(100);
        builder.Property(n => n.PedidoSapRegisteredAt);
        builder.Property(n => n.PedidoSapRegisteredByUserId);

        builder.Property(n => n.EtCreatedAt);
        builder.Property(n => n.EtCreatedByUserId);
        builder.Property(n => n.EtLoadedInSisproAt);
        builder.Property(n => n.EtLoadedInSisproByUserId);

        builder.OwnsOne(n => n.SuppliesTemporaryRejection, b =>
        {
            b.Property(x => x.Note).HasMaxLength(1000);
        });
        builder.OwnsOne(n => n.SuppliesAgreement, b =>
        {
            b.Property(x => x.Text).HasMaxLength(2000).IsRequired();
        });
        builder.OwnsOne(n => n.ClientRejection, b =>
        {
            b.Property(x => x.Comment).HasMaxLength(2000).IsRequired();
        });

        builder.OwnsOne(n => n.Capture, b =>
        {
            // Longitud por defecto para las ~68 columnas de captura (catalogadas en
            // npiFields.ts del frontend) — sin comportamiento de negocio propio, ver
            // docs/domain-model.md §2.2. Se afinan sólo las que requieren texto largo.
            foreach (var property in typeof(NpiCaptureData).GetProperties())
            {
                b.Property(property.Name).HasMaxLength(200);
            }

            b.Property(x => x.Observaciones).HasMaxLength(2000);
            b.Property(x => x.NotasEmpaqueEmbalaje).HasMaxLength(2000);
            b.HasIndex(x => x.NumeroCotizacion);
            b.HasIndex(x => x.Cliente);
        });

        ConfigureSatelliteCollection<Attachment>(builder, nameof(Npi.Attachments), "_attachments");
        ConfigureSatelliteCollection<ChangeRequest>(builder, nameof(Npi.ChangeRequests), "_changeRequests");
        ConfigureSatelliteCollection<InformationRequest>(builder, nameof(Npi.InformationRequests), "_informationRequests");
        ConfigureSatelliteCollection<FeasibilityReview>(builder, nameof(Npi.FeasibilityReviews), "_feasibilityReviews");
        ConfigureSatelliteCollection<PrintCardVersion>(builder, nameof(Npi.PrintCardVersions), "_printCardVersions");
        ConfigureSatelliteCollection<TimerEvent>(builder, nameof(Npi.TimerEventLog), "_timerEvents");
        ConfigureSatelliteCollection<AuditEvent>(builder, nameof(Npi.AuditLog), "_auditEvents");

        // Los eventos de dominio (IDomainEvent) nunca se persisten — se descartan tras
        // publicarse (Application, Fase 4+). Se ignoran explícitamente para el modelo de EF.
        builder.Ignore(n => n.DomainEvents);
    }

    /// <summary>
    /// Los 4 cronómetros son del mismo tipo CLR (TimerState) sobre el mismo dueño. Configurados
    /// como owned *entity* types (OwnsOne) esto disparaba un bug real de EF Core al construir el
    /// grafo de cambios: la FK sombra "NpiId" de un cronómetro terminaba aplicándose a la
    /// instancia de otro ("The property 'NpiId' belongs to the type 'Npi.DesignTimer#TimerState',
    /// but is being used with an instance of type 'Npi.CommercialTimer#TimerState'" — reproducido
    /// contra SQL Server real en Fase 8). TimerState no tiene identidad propia — se reemplaza
    /// entero en cada mutación (ver TimerState.cs) — así que es un candidato de manual para
    /// "complex type" (EF Core 8+): sin tipo de entidad propio, sin FK sombra, sin este problema
    /// de raíz. Mismos nombres de columna, sin migración nueva.
    /// </summary>
    private static void ConfigureTimer(
        EntityTypeBuilder<Npi> builder,
        System.Linq.Expressions.Expression<Func<Npi, TimerState?>> selector,
        string columnPrefix)
    {
        builder.ComplexProperty(selector, t =>
        {
            t.Property(x => x.StartAt).HasColumnName($"{columnPrefix}StartAt");
            t.Property(x => x.EndAt).HasColumnName($"{columnPrefix}EndAt");
            t.Property(x => x.AccumulatedBusinessMs).HasColumnName($"{columnPrefix}AccumulatedMs").IsRequired();
            t.Property(x => x.RunningSinceAt).HasColumnName($"{columnPrefix}RunningSinceAt");
        });
    }

    /// <summary>
    /// Configura una colección satélite del agregado usando el nombre de la propiedad de
    /// navegación (no una expresión lambda): el tipo de retorno de las navegaciones de Npi es
    /// IReadOnlyCollection&lt;T&gt; (de sólo lectura hacia afuera del agregado), que no admite
    /// la sobrecarga de HasMany basada en expresión — el nombre por string evita la ambigüedad
    /// y sigue apuntando al campo privado real vía HasField.
    /// </summary>
    private static void ConfigureSatelliteCollection<TSatellite>(
        EntityTypeBuilder<Npi> builder,
        string navigationPropertyName,
        string backingFieldName)
        where TSatellite : class
    {
        builder.HasMany<TSatellite>(navigationPropertyName)
            .WithOne()
            .HasForeignKey("NpiId")
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(navigationPropertyName)
            .HasField(backingFieldName)
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
