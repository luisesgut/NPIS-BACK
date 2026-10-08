using FluentValidation;
using NpiManagement.Application.Npis.Dtos;

namespace NpiManagement.Application.Npis.Validation;

/// <summary>
/// Longitudes máximas por defecto (200, salvo Observaciones y NotasEmpaqueEmbalaje a 2000) —
/// refleja exactamente los límites configurados en NpiConfiguration.OwnsOne(Capture) para fallar
/// rápido con 400 en vez de un error de base de datos. Generado por reflexión para no listar los
/// ~68 campos a mano (mismo criterio DRY que la configuración de EF).
/// </summary>
public sealed class NpiCaptureDtoValidator : AbstractValidator<NpiCaptureDto>
{
    private static readonly string[] LongTextFields = [nameof(NpiCaptureDto.Observaciones), nameof(NpiCaptureDto.NotasEmpaqueEmbalaje)];

    /// <summary>Unidades que el formulario ofrece; vacío = la unidad base (mm / micras).</summary>
    public static readonly string[] UnidadesMedidas = ["", "mm", "cm", "pulg"];
    public static readonly string[] UnidadesCalibre = ["", "micras", "galgas", "mils"];

    public NpiCaptureDtoValidator()
    {
        foreach (var property in typeof(NpiCaptureDto).GetProperties())
        {
            if (property.PropertyType != typeof(string)) continue;
            var maxLength = LongTextFields.Contains(property.Name) ? 2000 : 200;
            RuleFor(x => (string)property.GetValue(x)!)
                .MaximumLength(maxLength)
                .WithName(property.Name)
                .WithMessage($"{property.Name} no puede exceder {maxLength} caracteres.");
        }

        RuleFor(x => x.UnidadMedidas).Must(u => UnidadesMedidas.Contains(u))
            .WithMessage("Unidad de medidas no válida. Usa mm, cm o pulg.");
        RuleFor(x => x.UnidadCalibre).Must(u => UnidadesCalibre.Contains(u))
            .WithMessage("Unidad de calibre no válida. Usa micras, galgas o mils.");
    }
}
