using Microsoft.EntityFrameworkCore;
using NpiManagement.Application.Abstractions;
using NpiManagement.Domain;
using NpiManagement.Domain.ValueObjects;

namespace NpiManagement.Infrastructure.Persistence.Repositories;

/// <summary>
/// Único repositorio de la solución (específico del agregado Npi, no genérico — ver
/// docs/architecture-decisions.md ADR-017 y comentario en INpiRepository). Carga el agregado
/// completo con todas sus colecciones satélite; SaveChangesAsync del DbContext confirma la
/// transacción (Unit of Work).
/// </summary>
public sealed class NpiRepository(NpiManagementDbContext dbContext) : INpiRepository
{
    public async Task<Npi?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        await Query().FirstOrDefaultAsync(n => n.Id == id, cancellationToken);

    public async Task<Npi?> GetByNpiNumberAsync(string npiNumber, CancellationToken cancellationToken)
    {
        var number = NpiNumber.FromExisting(npiNumber);
        return await Query().FirstOrDefaultAsync(n => n.NpiNumber == number, cancellationToken);
    }

    public void Add(Npi npi) => dbContext.Npis.Add(npi);

    public byte[] GetRowVersion(Npi npi) =>
        (byte[])dbContext.Entry(npi).Property("RowVersion").CurrentValue!;

    public void SetExpectedRowVersion(Npi npi, byte[] expectedRowVersion) =>
        dbContext.Entry(npi).Property("RowVersion").OriginalValue = expectedRowVersion;

    private IQueryable<Npi> Query() =>
        dbContext.Npis
            .Include(n => n.Comments)
            .Include(n => n.Attachments)
            .Include(n => n.ChangeRequests)
            .Include(n => n.InformationRequests)
            .Include(n => n.FeasibilityReviews)
            .Include(n => n.PrintCardVersions).ThenInclude(v => v.Approvals)
            .Include(n => n.TimerEventLog)
            .Include(n => n.AuditLog);
}
