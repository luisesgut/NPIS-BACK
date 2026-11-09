using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NpiManagement.Application.Abstractions;
using NpiManagement.Application.Exceptions;
using NpiManagement.Domain.Common;
using NpiManagement.Infrastructure.Persistence;
using NpiManagement.Infrastructure.Persistence.Entities;

namespace NpiManagement.Infrastructure.Sispro;

public sealed class SisproAdminService(
    NpiManagementDbContext dbContext,
    ICatalogReader catalogReader,
    ISisproClient client,
    IOptions<SisproOptions> options,
    IClock clock) : ISisproAdminService
{
    public async Task<IReadOnlyList<SisproEquivalenceDto>> ListEquivalencesAsync(CancellationToken cancellationToken) =>
        (await dbContext.SisproMaterialEquivalences.AsNoTracking().OrderBy(e => e.MaterialSispro).ToListAsync(cancellationToken))
            .Select(ToDto)
            .ToList();

    public async Task<SisproEquivalenceDto> UpsertEquivalenceAsync(string materialSispro, string tipoPelicula, string? terminado, string? formulaLdpe, string actorName, CancellationToken cancellationToken)
    {
        var material = SisproQuotationMapper.Normalize(materialSispro);
        if (material.Length == 0)
            throw new RequestValidationException("sispro-material-required", "Escribe el material tal como aparece en SISPRO.");
        if (material.Length > 150)
            throw new RequestValidationException("sispro-material-too-long", "El nombre del material no puede exceder 150 caracteres.");

        var catalogs = await catalogReader.ListAsync(cancellationToken);
        var tipo = RequireOption(catalogs, ["TIPO DE PELICULA"], tipoPelicula, "Tipo de película")
            ?? throw new RequestValidationException("sispro-tipo-required", "Selecciona el tipo de película del NPI.");
        var terminadoValue = RequireOption(catalogs, ["TERMINADO", "TERMINADO LAMINACION"], terminado, "Terminado");
        var formulaValue = RequireOption(catalogs, ["FORMULA LDPE"], formulaLdpe, "Fórmula LDPE");

        var entity = await dbContext.SisproMaterialEquivalences.SingleOrDefaultAsync(e => e.MaterialSispro == material, cancellationToken);
        if (entity is null)
        {
            entity = new SisproMaterialEquivalence { Id = Guid.NewGuid(), MaterialSispro = material };
            dbContext.SisproMaterialEquivalences.Add(entity);
        }
        entity.TipoPelicula = tipo;
        entity.Terminado = terminadoValue;
        entity.FormulaLdpe = formulaValue;
        entity.UpdatedAt = clock.UtcNow;
        entity.UpdatedByName = actorName;

        await dbContext.SaveChangesAsync(cancellationToken);
        return ToDto(entity);
    }

    public async Task DeleteEquivalenceAsync(Guid id, CancellationToken cancellationToken)
    {
        var entity = await dbContext.SisproMaterialEquivalences.FindAsync([id], cancellationToken)
            ?? throw new NotFoundException("SisproMaterialEquivalence", id);
        dbContext.SisproMaterialEquivalences.Remove(entity);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<SisproStatusDto> CheckStatusAsync(CancellationToken cancellationToken)
    {
        if (!options.Value.IsConfigured)
            return new SisproStatusDto(false, false, "Falta capturar el usuario y la contraseña de SISPRO en el servidor (sección Sispro del web.config).");
        try
        {
            await client.CheckConnectionAsync(cancellationToken);
            return new SisproStatusDto(true, true, "Conexión con SISPRO correcta.");
        }
        catch (ExternalServiceUnavailableException ex)
        {
            return new SisproStatusDto(true, false, ex.Message);
        }
    }

    /// <summary>null si viene vacío; la opción tal cual del catálogo si existe (sin importar mayúsculas/acentos); 400 si no existe.</summary>
    private static string? RequireOption(IReadOnlyList<CatalogDto> catalogs, string[] catalogKeys, string? value, string label)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var wanted = SisproQuotationMapper.Normalize(value);
        var match = catalogs
            .Where(c => catalogKeys.Any(k => SisproQuotationMapper.Normalize(k) == SisproQuotationMapper.Normalize(c.Key)))
            .SelectMany(c => c.Options.Where(o => o.IsActive))
            .Select(o => o.Value)
            .FirstOrDefault(o => SisproQuotationMapper.Normalize(o) == wanted);
        return match ?? throw new RequestValidationException("sispro-option-not-in-catalog", $"{label}: «{value}» no existe en el catálogo del NPI.");
    }

    private static SisproEquivalenceDto ToDto(SisproMaterialEquivalence e) =>
        new(e.Id, e.MaterialSispro, e.TipoPelicula, e.Terminado, e.FormulaLdpe, e.UpdatedAt, e.UpdatedByName);
}
