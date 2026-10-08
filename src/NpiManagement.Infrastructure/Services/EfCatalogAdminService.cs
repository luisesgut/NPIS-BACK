using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using NpiManagement.Application.Abstractions;
using NpiManagement.Application.Authorization;
using NpiManagement.Application.Catalogs;
using NpiManagement.Application.Exceptions;
using NpiManagement.Domain;
using NpiManagement.Domain.Common;
using NpiManagement.Infrastructure.Persistence;
using NpiManagement.Infrastructure.Persistence.Entities;

namespace NpiManagement.Infrastructure.Services;

public sealed class EfCatalogAdminService(NpiManagementDbContext dbContext, IClock clock) : ICatalogAdminService
{
    private const int MaxValueLength = 150;

    public async Task<IReadOnlyList<CatalogAdminDto>> ListAsync(CancellationToken cancellationToken)
    {
        var catalogs = await dbContext.Catalogs
            .AsNoTracking()
            .Include(c => c.Options)
            .OrderBy(c => c.DisplayName)
            .ToListAsync(cancellationToken);

        var usage = await CountUsageAsync(cancellationToken);

        return catalogs.Select(c => new CatalogAdminDto(
            c.Id, c.Key, c.DisplayName, CatalogRules.Hint(c.Key),
            c.Options
                .OrderBy(o => o.DisplayOrder)
                .Select(o => new CatalogAdminOptionDto(
                    o.Id, o.Value, o.DisplayOrder, o.IsActive,
                    CatalogRules.ProtectionReason(c.Key, o.Value),
                    usage.GetValueOrDefault((c.Key.ToUpperInvariant(), o.Value))))
                .ToList()))
            .ToList();
    }

    public async Task<CatalogAdminOptionDto> AddOptionAsync(Guid catalogId, string value, CurrentUser actor, string actorName, CancellationToken cancellationToken)
    {
        var catalog = await dbContext.Catalogs.Include(c => c.Options).FirstOrDefaultAsync(c => c.Id == catalogId, cancellationToken)
            ?? throw new NotFoundException("Catalog", catalogId);
        var clean = ValidValue(value);
        EnsureUnique(catalog, clean, exceptOptionId: null);

        var option = new CatalogOption
        {
            Id = Guid.NewGuid(),
            CatalogId = catalog.Id,
            Value = clean,
            DisplayOrder = catalog.Options.Count == 0 ? 1 : catalog.Options.Max(o => o.DisplayOrder) + 1,
            IsActive = true,
        };
        dbContext.CatalogOptions.Add(option);
        Log(catalog, "Added", null, clean, actor, actorName);
        await dbContext.SaveChangesAsync(cancellationToken);

        return new CatalogAdminOptionDto(option.Id, option.Value, option.DisplayOrder, true, CatalogRules.ProtectionReason(catalog.Key, clean), 0);
    }

    public async Task RenameOptionAsync(Guid optionId, string value, CurrentUser actor, string actorName, CancellationToken cancellationToken)
    {
        var (catalog, option) = await LoadOptionAsync(optionId, cancellationToken);
        EnsureNotProtected(catalog, option, "renombrar");
        var clean = ValidValue(value);
        if (clean == option.Value) return;
        EnsureUnique(catalog, clean, option.Id);

        Log(catalog, "Renamed", option.Value, clean, actor, actorName);
        option.Value = clean;
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task SetActiveAsync(Guid optionId, bool isActive, CurrentUser actor, string actorName, CancellationToken cancellationToken)
    {
        var (catalog, option) = await LoadOptionAsync(optionId, cancellationToken);
        if (option.IsActive == isActive) return;
        if (!isActive) EnsureNotProtected(catalog, option, "desactivar");

        option.IsActive = isActive;
        Log(catalog, isActive ? "Activated" : "Deactivated", option.Value, option.Value, actor, actorName);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task ReorderAsync(Guid catalogId, IReadOnlyList<Guid> optionIds, CurrentUser actor, string actorName, CancellationToken cancellationToken)
    {
        var catalog = await dbContext.Catalogs.Include(c => c.Options).FirstOrDefaultAsync(c => c.Id == catalogId, cancellationToken)
            ?? throw new NotFoundException("Catalog", catalogId);

        // Se exige la lista completa: así otra persona que agregó una opción mientras tanto no queda fuera del orden.
        if (optionIds.Count != catalog.Options.Count || !catalog.Options.All(o => optionIds.Contains(o.Id)))
            throw new RequestValidationException("catalog-reorder-mismatch", "La lista de opciones cambió mientras la ordenabas. Recarga e inténtalo de nuevo.");

        for (var i = 0; i < optionIds.Count; i++)
            catalog.Options.Single(o => o.Id == optionIds[i]).DisplayOrder = i + 1;

        Log(catalog, "Reordered", null, null, actor, actorName);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<CatalogChangeDto>> ListChangesAsync(string? catalogKey, CancellationToken cancellationToken)
    {
        var query = dbContext.CatalogChangeLogs.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(catalogKey)) query = query.Where(c => c.CatalogKey == catalogKey);

        return await query
            .OrderByDescending(c => c.ChangedAt)
            .Take(200)
            .Select(c => new CatalogChangeDto(c.Id, c.CatalogKey, c.Action, c.OldValue, c.NewValue, c.ChangedByName, c.ChangedAt))
            .ToListAsync(cancellationToken);
    }

    private async Task<(Catalog Catalog, CatalogOption Option)> LoadOptionAsync(Guid optionId, CancellationToken cancellationToken)
    {
        var option = await dbContext.CatalogOptions.FirstOrDefaultAsync(o => o.Id == optionId, cancellationToken)
            ?? throw new NotFoundException("CatalogOption", optionId);
        var catalog = await dbContext.Catalogs.Include(c => c.Options).FirstAsync(c => c.Id == option.CatalogId, cancellationToken);
        return (catalog, option);
    }

    private static string ValidValue(string value)
    {
        var clean = CatalogRules.Clean(value ?? string.Empty);
        if (clean.Length == 0)
            throw new RequestValidationException("catalog-option-empty", "Escribe el texto de la opción.");
        if (clean.Length > MaxValueLength)
            throw new RequestValidationException("catalog-option-too-long", $"La opción no puede pasar de {MaxValueLength} caracteres.");
        return clean;
    }

    /// <summary>"Impresión" e "IMPRESION" serían la misma opción para quien captura: no se permiten ambas.</summary>
    private static void EnsureUnique(Catalog catalog, string value, Guid? exceptOptionId)
    {
        var normalized = CatalogRules.Normalize(value);
        var existing = catalog.Options.FirstOrDefault(o => o.Id != exceptOptionId && CatalogRules.Normalize(o.Value) == normalized);
        if (existing is not null)
            throw new RequestValidationException("catalog-option-duplicate",
                existing.IsActive
                    ? $"Ya existe la opción «{existing.Value}» en {catalog.DisplayName}."
                    : $"Ya existe la opción «{existing.Value}» en {catalog.DisplayName}, desactivada: actívala en lugar de crearla de nuevo.");
    }

    private static void EnsureNotProtected(Catalog catalog, CatalogOption option, string verb)
    {
        if (CatalogRules.ProtectionReason(catalog.Key, option.Value) is { } reason)
            throw new RequestValidationException("catalog-option-protected", $"No se puede {verb} «{option.Value}»: {reason}");
    }

    private void Log(Catalog catalog, string action, string? oldValue, string? newValue, CurrentUser actor, string actorName) =>
        dbContext.CatalogChangeLogs.Add(new CatalogChangeLog
        {
            Id = Guid.NewGuid(),
            CatalogId = catalog.Id,
            CatalogKey = catalog.Key,
            Action = action,
            OldValue = oldValue,
            NewValue = newValue,
            ChangedByUserId = actor.UserId,
            ChangedByName = actorName,
            ChangedAt = clock.UtcNow,
        });

    /// <summary>
    /// Cuántas veces aparece cada opción en las capturas: un GROUP BY por cada campo de la captura
    /// ligado a un catálogo (unas 27 consultas pequeñas; es una pantalla de administración).
    /// </summary>
    private async Task<Dictionary<(string CatalogKey, string Value), int>> CountUsageAsync(CancellationToken cancellationToken)
    {
        var counts = new Dictionary<(string, string), int>();
        foreach (var (catalogKey, fields) in CatalogRules.Fields)
        {
            foreach (var field in fields)
            {
                var npi = Expression.Parameter(typeof(Npi), "n");
                var selector = Expression.Lambda<Func<Npi, string>>(
                    Expression.Property(Expression.Property(npi, nameof(Npi.Capture)), field), npi);

                var rows = await dbContext.Npis.AsNoTracking()
                    .Select(selector)
                    .Where(v => v != null && v != "")
                    .GroupBy(v => v)
                    .Select(g => new { Value = g.Key, Count = g.Count() })
                    .ToListAsync(cancellationToken);

                foreach (var row in rows)
                {
                    var key = (catalogKey.ToUpperInvariant(), row.Value);
                    counts[key] = counts.GetValueOrDefault(key) + row.Count;
                }
            }
        }
        return counts;
    }
}
