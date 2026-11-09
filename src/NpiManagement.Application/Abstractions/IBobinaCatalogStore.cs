using NpiManagement.Application.Authorization;

namespace NpiManagement.Application.Abstractions;

/// <summary>
/// Material con densidad para el cálculo de bobina. Se liga a la captura por TipoPelicula + Terminado
/// (BOPP + MATE → "BOPP MATE"). Los registros EsEstimado son el valor provisional del PE según la
/// FormulaLDPE que captura Comercial; Ingeniería después elige el código exacto (CE-xx, M-xx…).
/// </summary>
public sealed record MaterialBobinaDto(
    Guid Id, string Codigo, double DensidadKgM3, string? TipoPelicula, string? Terminado, string? FormulaLdpe,
    bool EsEstimado, bool IsActive, string? UpdatedByName, DateTimeOffset? UpdatedAt);

/// <summary>Core ligado por nombre a la opción del catálogo CORE (3", 6"). Diámetro EXTERIOR en metros.</summary>
public sealed record CoreBobinaDto(Guid Id, string Nombre, double DiametroExteriorM, double KgPorCm, bool IsActive);

/// <summary>Valores de la especificación que planeación puede ajustar sin desplegar.</summary>
public sealed record ParametrosBobinaDto(
    double AdhesivoGm2, double TintaGm2, double TarimaAnchoCm, double TarimaLargoCm, double TarimaAltoMaxCm, double ToleranciaAlto);

public sealed record CatalogoBobinaDto(IReadOnlyList<MaterialBobinaDto> Materiales, IReadOnlyList<CoreBobinaDto> Cores, ParametrosBobinaDto Parametros);

/// <summary>Material exacto que eligió Ingeniería para una capa (PI, PPL, PSL) de un NPI.</summary>
public sealed record MaterialExactoDto(string Capa, Guid MaterialId, string Codigo, string SetByName, DateTimeOffset SetAt);

public sealed record MaterialBobinaInput(
    string Codigo, double DensidadKgM3, string? TipoPelicula, string? Terminado, string? FormulaLdpe, bool EsEstimado, bool IsActive);

public interface IBobinaCatalogStore
{
    Task<CatalogoBobinaDto> GetAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<MaterialExactoDto>> GetMaterialesExactosAsync(Guid npiId, CancellationToken cancellationToken);

    /// <summary>materialId null quita la elección y el cálculo vuelve a la densidad estimada.</summary>
    Task SetMaterialExactoAsync(Guid npiId, string capa, Guid? materialId, CurrentUser actor, string actorName, CancellationToken cancellationToken);

    /// <summary>id null crea un material nuevo.</summary>
    Task<MaterialBobinaDto> SaveMaterialAsync(Guid? id, MaterialBobinaInput input, CurrentUser actor, string actorName, CancellationToken cancellationToken);

    Task SaveCoreAsync(Guid id, double diametroExteriorM, double kgPorCm, bool isActive, CancellationToken cancellationToken);

    Task SaveParametrosAsync(ParametrosBobinaDto parametros, CancellationToken cancellationToken);
}
