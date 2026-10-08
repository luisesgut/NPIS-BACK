using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using NpiManagement.Domain;
using NpiManagement.Infrastructure.Identity;
using NpiManagement.Infrastructure.Persistence.Entities;

namespace NpiManagement.Infrastructure.Persistence;

/// <summary>
/// DbContext único de la solución. Actúa también como Unit of Work (SaveChangesAsync confirma
/// la transacción) — no existe una clase UoW separada, por instrucción explícita del pedido.
/// Hereda de IdentityDbContext para persistir Identity en el mismo SQL Server que el resto del
/// dominio (roles nativos de Identity, sin tabla de "Role" propia — ver ADR-020).
/// </summary>
public sealed class NpiManagementDbContext : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>
{
    public const string NpiNumberSequenceName = "NpiNumberSequence";
    public const string NpiNumberSequenceSchema = "dbo";

    public NpiManagementDbContext(DbContextOptions<NpiManagementDbContext> options) : base(options)
    {
    }

    public DbSet<Npi> Npis => Set<Npi>();
    public DbSet<Quotation> Quotations => Set<Quotation>();
    public DbSet<Catalog> Catalogs => Set<Catalog>();
    public DbSet<CatalogOption> CatalogOptions => Set<CatalogOption>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<FeedbackReport> FeedbackReports => Set<FeedbackReport>();
    public DbSet<CatalogChangeLog> CatalogChangeLogs => Set<CatalogChangeLog>();
    public DbSet<NpiManagement.Domain.Entities.NpiComment> NpiComments => Set<NpiManagement.Domain.Entities.NpiComment>();
    public DbSet<NpiChatRead> NpiChatReads => Set<NpiChatRead>();
    public DbSet<NpiManagement.Domain.Entities.CaptureChange> NpiCaptureChanges => Set<NpiManagement.Domain.Entities.CaptureChange>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Numeración atómica de NPI (ADR-005): NEXT VALUE FOR dbo.NpiNumberSequence.
        // Arranca en 1; los NPIs semilla de desarrollo (NPI-1000+) se insertan con número
        // explícito, fuera de este rango.
        modelBuilder.HasSequence<int>(NpiNumberSequenceName, NpiNumberSequenceSchema)
            .StartsAt(1)
            .IncrementsBy(1);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(NpiManagementDbContext).Assembly);
    }
}
