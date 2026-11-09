namespace NpiManagement.Infrastructure.Persistence.Entities;

/// <summary>
/// Densidad de un material para el cálculo peso ↔ diámetro de bobina. Semilla: las 98 filas del Excel
/// "Convertidor de peso a diámetro y viceversa (bobina)" + estimados de PE por fórmula LDPE.
/// </summary>
public sealed class MaterialBobina
{
    public Guid Id { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public decimal DensidadKgM3 { get; set; }
    /// <summary>Opción del catálogo TIPO DE PELICULA a la que corresponde (BOPP, PET, PE…).</summary>
    public string? TipoPelicula { get; set; }
    /// <summary>Opción de TERMINADO / TERMINADO LAMINACION (MATE, METALIZADO…); null = cualquier terminado.</summary>
    public string? Terminado { get; set; }
    /// <summary>Solo en estimados de PE: opción de FORMULA LDPE; null = estimado general.</summary>
    public string? FormulaLdpe { get; set; }
    public bool EsEstimado { get; set; }
    public bool IsActive { get; set; } = true;
    public string? UpdatedByName { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
}

/// <summary>Core ligado por nombre al catálogo CORE. Diámetro EXTERIOR (3" = 0.095 m).</summary>
public sealed class CoreBobina
{
    public Guid Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public decimal DiametroExteriorM { get; set; }
    public decimal KgPorCm { get; set; }
    public bool IsActive { get; set; } = true;
}

/// <summary>Una sola fila (Id = 1) con los parámetros ajustables de la especificación.</summary>
public sealed class ParametrosBobina
{
    public int Id { get; set; }
    public decimal AdhesivoGm2 { get; set; }
    public decimal TintaGm2 { get; set; }
    public decimal TarimaAnchoCm { get; set; }
    public decimal TarimaLargoCm { get; set; }
    public decimal TarimaAltoMaxCm { get; set; }
    public decimal ToleranciaAlto { get; set; }
}

/// <summary>Código exacto que eligió Ingeniería para una capa del NPI (PI, PPL o PSL).</summary>
public sealed class NpiMaterialExacto
{
    public Guid NpiId { get; set; }
    public string Capa { get; set; } = string.Empty;
    public Guid MaterialBobinaId { get; set; }
    public Guid SetByUserId { get; set; }
    public string SetByName { get; set; } = string.Empty;
    public DateTimeOffset SetAt { get; set; }

    public MaterialBobina Material { get; set; } = null!;
}
