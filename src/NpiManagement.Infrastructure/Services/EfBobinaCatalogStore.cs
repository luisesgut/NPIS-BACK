using Microsoft.EntityFrameworkCore;
using NpiManagement.Application.Abstractions;
using NpiManagement.Application.Authorization;
using NpiManagement.Application.Catalogs;
using NpiManagement.Application.Exceptions;
using NpiManagement.Domain.Common;
using NpiManagement.Infrastructure.Persistence;
using NpiManagement.Infrastructure.Persistence.Entities;

namespace NpiManagement.Infrastructure.Services;

public sealed class EfBobinaCatalogStore(NpiManagementDbContext dbContext, IClock clock) : IBobinaCatalogStore
{
    private static readonly string[] Capas = ["PI", "PPL", "PSL"];

    /// <summary>Si la fila de parámetros no existe todavía, se usan los valores de la especificación.</summary>
    private static readonly ParametrosBobinaDto ParametrosPorDefecto = new(1.7, 2.0, 100, 120, 150, 0);

    public async Task<CatalogoBobinaDto> GetAsync(CancellationToken cancellationToken)
    {
        var materiales = await dbContext.MaterialesBobina.AsNoTracking().OrderBy(m => m.Codigo).ToListAsync(cancellationToken);
        var cores = await dbContext.CoresBobina.AsNoTracking().OrderBy(c => c.Nombre).ToListAsync(cancellationToken);
        var p = await dbContext.ParametrosBobina.AsNoTracking().FirstOrDefaultAsync(cancellationToken);

        return new CatalogoBobinaDto(
            materiales.Select(ToDto).ToList(),
            cores.Select(c => new CoreBobinaDto(c.Id, c.Nombre, (double)c.DiametroExteriorM, (double)c.KgPorCm, c.IsActive)).ToList(),
            p is null ? ParametrosPorDefecto : new ParametrosBobinaDto(
                (double)p.AdhesivoGm2, (double)p.TintaGm2, (double)p.TarimaAnchoCm, (double)p.TarimaLargoCm, (double)p.TarimaAltoMaxCm, (double)p.ToleranciaAlto));
    }

    public async Task<IReadOnlyList<MaterialExactoDto>> GetMaterialesExactosAsync(Guid npiId, CancellationToken cancellationToken) =>
        await dbContext.NpiMaterialesExactos.AsNoTracking()
            .Where(e => e.NpiId == npiId)
            .Select(e => new MaterialExactoDto(e.Capa, e.MaterialBobinaId, e.Material.Codigo, e.SetByName, e.SetAt))
            .ToListAsync(cancellationToken);

    public async Task SetMaterialExactoAsync(Guid npiId, string capa, Guid? materialId, CurrentUser actor, string actorName, CancellationToken cancellationToken)
    {
        capa = (capa ?? "").Trim().ToUpperInvariant();
        if (!Capas.Contains(capa))
            throw new RequestValidationException("bobina-capa-invalida", "La capa debe ser PI, PPL o PSL.");
        if (!await dbContext.Npis.AnyAsync(n => n.Id == npiId, cancellationToken))
            throw new NotFoundException("Npi", npiId);

        var actual = await dbContext.NpiMaterialesExactos.FirstOrDefaultAsync(e => e.NpiId == npiId && e.Capa == capa, cancellationToken);
        if (materialId is null)
        {
            if (actual is not null) dbContext.NpiMaterialesExactos.Remove(actual);
        }
        else
        {
            var material = await dbContext.MaterialesBobina.FirstOrDefaultAsync(m => m.Id == materialId, cancellationToken)
                ?? throw new NotFoundException("MaterialBobina", materialId);
            if (material.EsEstimado || !material.IsActive || material.DensidadKgM3 <= 0)
                throw new RequestValidationException("bobina-material-no-elegible", $"{material.Codigo} no se puede elegir como material exacto.");

            if (actual is null)
            {
                actual = new NpiMaterialExacto { NpiId = npiId, Capa = capa };
                dbContext.NpiMaterialesExactos.Add(actual);
            }
            actual.MaterialBobinaId = material.Id;
            actual.SetByUserId = actor.UserId;
            actual.SetByName = actorName;
            actual.SetAt = clock.UtcNow;
        }
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<MaterialBobinaDto> SaveMaterialAsync(Guid? id, MaterialBobinaInput input, CurrentUser actor, string actorName, CancellationToken cancellationToken)
    {
        var codigo = CatalogRules.Clean(input.Codigo ?? "");
        if (codigo.Length is 0 or > 60)
            throw new RequestValidationException("bobina-codigo-invalido", "El código es obligatorio (máximo 60 caracteres).");
        if (input.DensidadKgM3 is <= 0 or > 25_000)
            throw new RequestValidationException("bobina-densidad-invalida", "La densidad debe estar entre 0 y 25,000 kg/m³.");

        var normalizado = CatalogRules.Normalize(codigo);
        var existentes = await dbContext.MaterialesBobina.Where(m => m.Id != id).Select(m => m.Codigo).ToListAsync(cancellationToken);
        if (existentes.Any(c => CatalogRules.Normalize(c) == normalizado))
            throw new RequestValidationException("bobina-codigo-duplicado", $"Ya existe el material «{codigo}».");

        MaterialBobina material;
        if (id is null)
        {
            material = new MaterialBobina { Id = Guid.NewGuid() };
            dbContext.MaterialesBobina.Add(material);
        }
        else
        {
            material = await dbContext.MaterialesBobina.FirstOrDefaultAsync(m => m.Id == id, cancellationToken)
                ?? throw new NotFoundException("MaterialBobina", id);
        }

        material.Codigo = codigo;
        material.DensidadKgM3 = (decimal)input.DensidadKgM3;
        material.TipoPelicula = Opcional(input.TipoPelicula);
        material.Terminado = Opcional(input.Terminado);
        material.FormulaLdpe = Opcional(input.FormulaLdpe);
        material.EsEstimado = input.EsEstimado;
        material.IsActive = input.IsActive;
        material.UpdatedByName = actorName;
        material.UpdatedAt = clock.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
        return ToDto(material);
    }

    public async Task SaveCoreAsync(Guid id, double diametroExteriorM, double kgPorCm, bool isActive, CancellationToken cancellationToken)
    {
        if (diametroExteriorM is <= 0 or > 1)
            throw new RequestValidationException("bobina-core-invalido", "El diámetro exterior del core va en metros (ej. 0.095).");
        if (kgPorCm is < 0 or > 1)
            throw new RequestValidationException("bobina-core-invalido", "El peso del core va en kg por cm de ancho (ej. 0.0188).");

        var core = await dbContext.CoresBobina.FirstOrDefaultAsync(c => c.Id == id, cancellationToken)
            ?? throw new NotFoundException("CoreBobina", id);
        core.DiametroExteriorM = (decimal)diametroExteriorM;
        core.KgPorCm = (decimal)kgPorCm;
        core.IsActive = isActive;
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task SaveParametrosAsync(ParametrosBobinaDto p, CancellationToken cancellationToken)
    {
        if (p.AdhesivoGm2 < 0 || p.TintaGm2 < 0 || p.TarimaAnchoCm <= 0 || p.TarimaLargoCm <= 0 || p.TarimaAltoMaxCm <= 0 || p.ToleranciaAlto is < 0 or > 1)
            throw new RequestValidationException("bobina-parametros-invalidos", "Revisa los parámetros: medidas mayores a 0 y tolerancia entre 0 y 1.");

        var fila = await dbContext.ParametrosBobina.FirstOrDefaultAsync(cancellationToken);
        if (fila is null)
        {
            fila = new ParametrosBobina { Id = 1 };
            dbContext.ParametrosBobina.Add(fila);
        }
        fila.AdhesivoGm2 = (decimal)p.AdhesivoGm2;
        fila.TintaGm2 = (decimal)p.TintaGm2;
        fila.TarimaAnchoCm = (decimal)p.TarimaAnchoCm;
        fila.TarimaLargoCm = (decimal)p.TarimaLargoCm;
        fila.TarimaAltoMaxCm = (decimal)p.TarimaAltoMaxCm;
        fila.ToleranciaAlto = (decimal)p.ToleranciaAlto;
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static string? Opcional(string? value) => string.IsNullOrWhiteSpace(value) ? null : CatalogRules.Clean(value);

    private static MaterialBobinaDto ToDto(MaterialBobina m) =>
        new(m.Id, m.Codigo, (double)m.DensidadKgM3, m.TipoPelicula, m.Terminado, m.FormulaLdpe, m.EsEstimado, m.IsActive, m.UpdatedByName, m.UpdatedAt);
}
