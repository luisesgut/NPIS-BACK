using System.Globalization;
using System.Text.RegularExpressions;
using NpiManagement.Application.Abstractions;
using NpiManagement.Application.Catalogs;
using NpiManagement.Application.Exceptions;
using NpiManagement.Application.Npis.Dtos;
using NpiManagement.Domain.Calculos;

namespace NpiManagement.Application.Bobinas;

public static class ModoCalculo
{
    public const string DesdeDiametro = "diametro";
    public const string DesdePeso = "peso";
}

public sealed record CapaCalculadaDto(string Capa, string Material, string Codigo, double DensidadKgM3, double Micras, string Origen);

public sealed record TarimaCalculadaDto(double AnchoCm, double LargoCm, double AltoMaxCm, int PorCama, int Camas, int Total, double AltoCm, double PesoKg);

/// <summary>Resultado sin redondear: el front redondea al mostrar (2 decimales kg/cm, 0 metros/piezas).</summary>
public sealed record CalculoBobinaDto(
    string Modo, double G, double TTotMicras, int Laminaciones, double FactorTinta, string Core, double AnchoMm,
    double DiametroCm, double PesoNetoKg, double PesoBrutoKg, double MetrosLineales, double? Piezas,
    TarimaCalculadaDto Tarima, IReadOnlyList<CapaCalculadaDto> Capas, IReadOnlyList<string> Avisos);

public sealed record CapaLibre(Guid MaterialId, double Micras);

/// <summary>Calculadora sin NPI: capas elegidas directo del catálogo de densidades.</summary>
public sealed record CalculoLibreRequest(
    IReadOnlyList<CapaLibre> Capas, double FactorTinta, string Core, double AnchoMm, string Modo, double Valor,
    double? RepeticionMm, double? TarimaAnchoCm, double? TarimaLargoCm, double? TarimaAltoMaxCm);

/// <summary>
/// Calcula sobre la captura tal como está en el formulario (aunque no se haya guardado). Con NpiId se
/// usan además los códigos exactos que eligió Ingeniería. El valor de entrada sale de DiametroBobina
/// (cm) o PesoBobina (kg) según el modo.
/// </summary>
public sealed record CalculoCapturaRequest(Guid? NpiId, NpiCaptureDto Capture, string Modo);

/// <summary>
/// Arma la entrada del cálculo de bobina (Domain.Calculos) desde la captura o desde la calculadora
/// libre: resuelve densidades, convierte unidades (mm → m, cm → m) y aplica los parámetros del catálogo.
/// </summary>
public sealed partial class BobinaCalculatorService(IBobinaCatalogStore store)
{
    private const string SinImpresion = "SIN IMPRESION";

    public async Task<CalculoBobinaDto> CalcularCapturaAsync(CalculoCapturaRequest request, CancellationToken cancellationToken)
    {
        var catalogo = await store.GetAsync(cancellationToken);
        var exactos = request.NpiId is { } npiId ? await store.GetMaterialesExactosAsync(npiId, cancellationToken) : [];
        var c = request.Capture;
        var errores = new List<string>();
        var avisos = new List<string>();

        var capas = new List<CapaResuelta>();
        foreach (var capa in CapasDeCaptura(c, errores))
        {
            var exactoId = exactos.FirstOrDefault(e => e.Capa == capa.Capa)?.MaterialId;
            var exacto = exactoId is null ? null : catalogo.Materiales.FirstOrDefault(m => m.Id == exactoId);
            var resuelta = ResolucionMaterial.Resolver(capa, catalogo.Materiales, exacto, out var error);
            if (resuelta is null) { errores.Add(error!); continue; }
            capas.Add(resuelta);
            if (resuelta.Aviso is not null) avisos.Add(resuelta.Aviso);
        }
        if (capas.Count == 0 && errores.Count == 0)
            errores.Add("Captura al menos la película de impresión (tipo y calibre) en Producto y estructura.");

        var core = BuscarCore(catalogo, c.Core, errores);
        var anchoMm = Numero(c.Ancho);
        if (anchoMm is not > 0) errores.Add("Captura el ancho de la bobina.");

        var valor = request.Modo == ModoCalculo.DesdeDiametro ? Numero(c.DiametroBobina) : Numero(c.PesoBobina);
        if (request.Modo is not (ModoCalculo.DesdeDiametro or ModoCalculo.DesdePeso))
            errores.Add("Modo de cálculo inválido.");
        else if (valor is not > 0)
            errores.Add(request.Modo == ModoCalculo.DesdeDiametro ? "Captura el diámetro de la bobina (cm)." : "Captura el peso por bobina (kg).");

        LanzarSiHayErrores(errores);

        var factorTinta = SinTinta(c) ? 0 : 1;
        var tarima = TarimaDeCaptura(c, catalogo.Parametros);
        var valorM = request.Modo == ModoCalculo.DesdeDiametro ? valor!.Value / 100 : valor!.Value;
        return Calcular(request.Modo, capas, factorTinta, core!, anchoMm!.Value, valorM, Numero(c.Repeticion), tarima, catalogo.Parametros, avisos);
    }

    public async Task<CalculoBobinaDto> CalcularLibreAsync(CalculoLibreRequest request, CancellationToken cancellationToken)
    {
        var catalogo = await store.GetAsync(cancellationToken);
        var errores = new List<string>();
        var capas = new List<CapaResuelta>();
        var nombres = new[] { "Capa 1", "Capa 2", "Capa 3", "Capa 4", "Capa 5" };

        if (request.Capas is not { Count: > 0 }) errores.Add("Agrega al menos una capa.");
        else if (request.Capas.Count > nombres.Length) errores.Add($"Máximo {nombres.Length} capas.");
        else
        {
            for (var i = 0; i < request.Capas.Count; i++)
            {
                var capa = request.Capas[i];
                var material = catalogo.Materiales.FirstOrDefault(m => m.Id == capa.MaterialId);
                if (material is null) errores.Add($"{nombres[i]}: material no encontrado.");
                else if (material.DensidadKgM3 <= 0) errores.Add($"{nombres[i]}: {material.Codigo} no tiene densidad válida.");
                else if (capa.Micras <= 0) errores.Add($"{nombres[i]}: captura el calibre.");
                else capas.Add(new(nombres[i], material.Codigo, material, capa.Micras, OrigenDensidad.Exacto, null));
            }
        }

        var core = BuscarCore(catalogo, request.Core, errores);
        if (request.AnchoMm <= 0) errores.Add("Captura el ancho de la bobina.");
        if (request.FactorTinta < 0) errores.Add("El factor de tinta no puede ser negativo.");
        if (request.Modo is not (ModoCalculo.DesdeDiametro or ModoCalculo.DesdePeso)) errores.Add("Modo de cálculo inválido.");
        else if (request.Valor <= 0) errores.Add(request.Modo == ModoCalculo.DesdeDiametro ? "Captura el diámetro (cm)." : "Captura el peso (kg).");
        LanzarSiHayErrores(errores);

        var p = catalogo.Parametros;
        var tarima = new TarimaBobinas(
            request.TarimaAnchoCm is > 0 ? request.TarimaAnchoCm.Value : p.TarimaAnchoCm,
            request.TarimaLargoCm is > 0 ? request.TarimaLargoCm.Value : p.TarimaLargoCm,
            request.TarimaAltoMaxCm is > 0 ? request.TarimaAltoMaxCm.Value : p.TarimaAltoMaxCm,
            p.ToleranciaAlto);
        var valorM = request.Modo == ModoCalculo.DesdeDiametro ? request.Valor / 100 : request.Valor;
        return Calcular(request.Modo, capas, request.FactorTinta, core!, request.AnchoMm, valorM, request.RepeticionMm, tarima, p, []);
    }

    private static CalculoBobinaDto Calcular(
        string modo, IReadOnlyList<CapaResuelta> capas, double factorTinta, CoreBobinaDto core, double anchoMm, double valor,
        double? repeticionMm, TarimaBobinas tarima, ParametrosBobinaDto p, List<string> avisos)
    {
        var estructura = new EstructuraBobina(
            capas.Select(c => new CapaBobina(c.Densidad.Codigo, c.Densidad.DensidadKgM3, c.Micras)).ToList(),
            factorTinta, p.AdhesivoGm2, p.TintaGm2);
        var coreDominio = new CoreBobina(core.Nombre, core.DiametroExteriorM, core.KgPorCm);
        var anchoM = anchoMm / 1000;
        var repeticionM = repeticionMm is > 0 ? repeticionMm / 1000 : null;

        var r = modo == ModoCalculo.DesdeDiametro
            ? CalculoBobina.DesdeDiametro(estructura, coreDominio, anchoM, valor, repeticionM)
            : CalculoBobina.DesdePeso(estructura, coreDominio, anchoM, valor, repeticionM);
        var t = CalculoTarima.Calcular(tarima, r.DiametroM * 100, anchoM * 100, r.PesoBrutoKg);

        if (repeticionM is null) avisos.Add("Sin repetición no se calculan piezas por bobina.");
        if (t.PorCama == 0) avisos.Add("La bobina es más grande que la base de la tarima.");

        return new CalculoBobinaDto(
            modo, r.G, r.TTot * 1_000_000, Math.Max(0, capas.Count - 1), factorTinta, core.Nombre, anchoMm,
            r.DiametroM * 100, r.PesoNetoKg, r.PesoBrutoKg, r.MetrosLineales, r.Piezas,
            new TarimaCalculadaDto(tarima.AnchoCm, tarima.LargoCm, tarima.AltoMaxCm, t.PorCama, t.Camas, t.Total, t.AltoCm, t.PesoKg),
            capas.Select(c => new CapaCalculadaDto(c.Capa, c.Material, c.Densidad.Codigo, c.Densidad.DensidadKgM3, c.Micras, c.Origen)).ToList(),
            avisos);
    }

    /// <summary>PI, PPL y PSL con tipo de película capturado; el calibre ya viene en micras (la captura lo convierte).</summary>
    private static IEnumerable<CapaCaptura> CapasDeCaptura(NpiCaptureDto c, List<string> errores)
    {
        var capas = new[]
        {
            ("PI", c.PiTipoPelicula, c.TerminadoPI, c.FormulaLDPE_PI, c.PiCalibre),
            ("PPL", c.PplTipoPelicula, c.TerminadoPPL, c.FormulaLDPE_PPL, c.PplCalibre),
            ("PSL", c.PslTipoPelicula, c.TerminadoPSL, c.FormulaLDPE_PSL, c.PslCalibre),
        };
        foreach (var (capa, tipo, terminado, formula, calibre) in capas)
        {
            if (ResolucionMaterial.SinValor(tipo)) continue;
            var micras = Numero(calibre);
            if (micras is not > 0) { errores.Add($"{capa}: captura el calibre de {tipo}."); continue; }
            yield return new CapaCaptura(capa, tipo, terminado, formula, micras.Value);
        }
    }

    private static bool SinTinta(NpiCaptureDto c) =>
        CatalogRules.Normalize(c.Acabado) == SinImpresion || ResolucionMaterial.SinValor(c.TipoImpresion) && !string.IsNullOrWhiteSpace(c.TipoImpresion);

    private static CoreBobinaDto? BuscarCore(CatalogoBobinaDto catalogo, string nombre, List<string> errores)
    {
        if (string.IsNullOrWhiteSpace(nombre)) { errores.Add("Selecciona el core."); return null; }
        var core = catalogo.Cores.FirstOrDefault(x => x.IsActive && CatalogRules.Normalize(x.Nombre) == CatalogRules.Normalize(nombre));
        if (core is null) errores.Add($"El core {nombre} no tiene diámetro ni peso registrados. Agrégalo en Catálogos › Densidades.");
        return core;
    }

    /// <summary>Medidas de tarima de la captura (en m, ej. "1.20 x 1.00") o, si no hay, las del catálogo.</summary>
    private static TarimaBobinas TarimaDeCaptura(NpiCaptureDto c, ParametrosBobinaDto p)
    {
        var medidas = NumeroRegex().Matches(c.DimTarimaBobina ?? "").Select(m => Numero(m.Value)).Where(v => v is > 0).Select(v => ACm(v!.Value)).ToList();
        var alto = Numero(c.AlturaTarimaMaxBobina);
        return new TarimaBobinas(
            medidas.Count >= 2 ? medidas[0] : p.TarimaAnchoCm,
            medidas.Count >= 2 ? medidas[1] : p.TarimaLargoCm,
            alto is > 0 ? ACm(alto.Value) : p.TarimaAltoMaxCm,
            p.ToleranciaAlto);
    }

    /// <summary>La captura pide metros; si alguien escribió 120 en vez de 1.20 se toma como cm.</summary>
    private static double ACm(double valor) => valor <= 10 ? valor * 100 : valor;

    private static double? Numero(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto)) return null;
        var limpio = texto.Trim();
        if (!limpio.Contains('.') && limpio.Count(ch => ch == ',') == 1) limpio = limpio.Replace(',', '.');
        return double.TryParse(limpio, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : null;
    }

    private static void LanzarSiHayErrores(List<string> errores)
    {
        if (errores.Count > 0)
            throw new RequestValidationException("bobina-datos-incompletos", string.Join(" ", errores));
    }

    [GeneratedRegex(@"\d+(?:[.,]\d+)?")]
    private static partial Regex NumeroRegex();
}
