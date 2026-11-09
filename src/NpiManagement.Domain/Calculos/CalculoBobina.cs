using NpiManagement.Domain.Exceptions;

namespace NpiManagement.Domain.Calculos;

/// <summary>Una capa (sustrato) de la estructura: densidad del catálogo y calibre capturado.</summary>
public sealed record CapaBobina(string Material, double DensidadKgM3, double Micras);

/// <summary>Core de cartón: diámetro EXTERIOR (3" = 0.095 m, 6" = 0.1712 m) y peso por cm de ancho.</summary>
public sealed record CoreBobina(string Nombre, double DiametroExteriorM, double KgPorCm);

/// <summary>
/// Estructura completa. Laminaciones = capas − 1 (cada unión lleva adhesivo). Adhesivo y tinta
/// suman masa a G pero no espesor a T_tot, igual que el PDF "Cálculos SISPRO (Diámetro-Peso)".
/// </summary>
public sealed record EstructuraBobina(
    IReadOnlyList<CapaBobina> Capas,
    double FactorTinta,
    double AdhesivoGm2 = 1.7,
    double TintaGm2 = 2.0);

/// <summary>Resultados teóricos sin redondear (redondear solo al mostrar).</summary>
public sealed record ResultadoBobina(
    double G,
    double TTot,
    double DiametroM,
    double PesoNetoKg,
    double PesoBrutoKg,
    double MetrosLineales,
    double? Piezas);

/// <summary>
/// Conversión peso ↔ diámetro de bobina (especificación "Cálculos de bobina: peso ↔ diámetro").
/// Función pura: unidades internas en metros y kilogramos; no toca base de datos. Los metros lineales
/// restan el peso del core igual que el Excel original (decisión del negocio para que los números cuadren).
/// </summary>
public static class CalculoBobina
{
    public static (double G, double TTot) MasaYEspesor(EstructuraBobina estructura)
    {
        if (estructura.Capas.Count == 0)
            throw new NpiValidationException("bobina-sin-capas", "La estructura no tiene capas.");
        foreach (var capa in estructura.Capas)
        {
            if (capa.DensidadKgM3 <= 0)
                throw new NpiValidationException("bobina-densidad-invalida", $"El material '{capa.Material}' no tiene una densidad válida.");
            if (capa.Micras <= 0)
                throw new NpiValidationException("bobina-calibre-invalido", $"El calibre de '{capa.Material}' debe ser mayor a 0.");
        }
        if (estructura.FactorTinta < 0 || estructura.AdhesivoGm2 < 0 || estructura.TintaGm2 < 0)
            throw new NpiValidationException("bobina-aporte-invalido", "Los aportes de tinta y adhesivo no pueden ser negativos.");

        int laminaciones = estructura.Capas.Count - 1;
        double tTot = estructura.Capas.Sum(c => c.Micras / 1_000_000d);
        double g = estructura.Capas.Sum(c => c.DensidadKgM3 * c.Micras / 1_000_000d)
                   + estructura.AdhesivoGm2 / 1000d * laminaciones
                   + estructura.TintaGm2 / 1000d * estructura.FactorTinta;
        return (g, tTot);
    }

    public static ResultadoBobina DesdeDiametro(EstructuraBobina estructura, CoreBobina core, double anchoM, double diametroM, double? repeticionM = null)
    {
        ValidarAncho(anchoM);
        if (diametroM <= core.DiametroExteriorM)
            throw new NpiValidationException("bobina-diametro-menor-core", $"El diámetro debe ser mayor que el del core {core.Nombre} ({core.DiametroExteriorM * 100:0.##} cm).");

        var (g, t) = MasaYEspesor(estructura);
        double d = core.DiametroExteriorM;
        double m = Math.PI * (diametroM * diametroM - d * d) * anchoM * g / (4 * t);
        return Armar(g, t, core, anchoM, diametroM, m, repeticionM);
    }

    public static ResultadoBobina DesdePeso(EstructuraBobina estructura, CoreBobina core, double anchoM, double pesoNetoKg, double? repeticionM = null)
    {
        ValidarAncho(anchoM);
        if (pesoNetoKg <= 0)
            throw new NpiValidationException("bobina-peso-invalido", "El peso de la bobina debe ser mayor a 0.");

        var (g, t) = MasaYEspesor(estructura);
        double d = core.DiametroExteriorM;
        double diametro = Math.Sqrt(d * d + 4 * pesoNetoKg * t / (Math.PI * anchoM * g));
        return Armar(g, t, core, anchoM, diametro, pesoNetoKg, repeticionM);
    }

    private static void ValidarAncho(double anchoM)
    {
        if (anchoM <= 0)
            throw new NpiValidationException("bobina-ancho-invalido", "El ancho de la bobina debe ser mayor a 0.");
    }

    private static ResultadoBobina Armar(double g, double t, CoreBobina core, double anchoM, double diametroM, double pesoNetoKg, double? repeticionM)
    {
        double pesoCoreKg = core.KgPorCm * (anchoM * 100);
        double metros = Math.Max(0, pesoNetoKg - pesoCoreKg) / (g * anchoM);
        double bruto = pesoNetoKg + pesoCoreKg;
        double? piezas = repeticionM is > 0 ? metros / repeticionM : null;
        return new ResultadoBobina(g, t, diametroM, pesoNetoKg, bruto, metros, piezas);
    }
}

/// <summary>Medidas de la tarima en cm. La tolerancia permite rebasar el alto máximo (0.10 = 10 %); por defecto no.</summary>
public sealed record TarimaBobinas(double AnchoCm = 100, double LargoCm = 120, double AltoMaxCm = 150, double ToleranciaAlto = 0);

public sealed record ResultadoTarima(int PorCama, int Camas, int Total, double AltoCm, double PesoKg);

/// <summary>
/// Bobinas paradas (eje vertical): cada una ocupa D × D en la base y cada cama mide el ancho de alto.
/// Siempre trunca hacia abajo: una bobina que no cabe completa no cabe (el Excel redondeaba).
/// </summary>
public static class CalculoTarima
{
    public static ResultadoTarima Calcular(TarimaBobinas tarima, double diametroCm, double anchoCm, double pesoBrutoKg)
    {
        if (diametroCm <= 0 || anchoCm <= 0)
            throw new NpiValidationException("tarima-medidas-invalidas", "El diámetro y el ancho de la bobina deben ser mayores a 0.");

        int porCama = (int)Math.Floor(tarima.AnchoCm / diametroCm) * (int)Math.Floor(tarima.LargoCm / diametroCm);
        double altoPermitido = tarima.AltoMaxCm * (1 + tarima.ToleranciaAlto);
        int camas = Math.Max(1, (int)Math.Floor(altoPermitido / anchoCm));
        int total = porCama * camas;
        return new ResultadoTarima(porCama, camas, total, camas * anchoCm, total * pesoBrutoKg);
    }
}
