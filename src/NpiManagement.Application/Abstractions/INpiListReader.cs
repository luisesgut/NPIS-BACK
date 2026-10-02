using System.Linq.Expressions;
using NpiManagement.Application.Npis.Dtos;
using NpiManagement.Application.Npis.Queries;
using NpiManagement.Domain;

namespace NpiManagement.Application.Abstractions;

/// <summary>
/// Lectura de listado paginada, separada del repositorio del agregado (CQRS pragmático — ADR-017):
/// lista sólo campos resumidos, sin cargar las colecciones satélite completas de cada NPI, y
/// filtra desde la base de datos (nunca carga todo y filtra en memoria — authorization-matrix.md §0).
/// </summary>
public interface INpiListReader
{
    Task<PagedResult<NpiListItemDto>> ListAsync(NpiListFilter filter, Expression<Func<Npi, bool>> accessPredicate, CancellationToken cancellationToken);
}
