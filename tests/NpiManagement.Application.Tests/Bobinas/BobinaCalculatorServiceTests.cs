using FluentAssertions;
using NpiManagement.Application.Abstractions;
using NpiManagement.Application.Authorization;
using NpiManagement.Application.Bobinas;
using NpiManagement.Application.Exceptions;
using NpiManagement.Application.Npis.Dtos;
using Xunit;

namespace NpiManagement.Application.Tests.Bobinas;

/// <summary>Arma el cálculo de bobina desde la captura: densidades, unidades (mm/cm) y tarima.</summary>
public class BobinaCalculatorServiceTests
{
    private static readonly MaterialBobinaDto Bopp = Material("BOPP", 910, "BOPP");
    private static readonly MaterialBobinaDto BoppMate = Material("BOPP MATE", 910, "BOPP", "MATE");
    private static readonly MaterialBobinaDto BoppMet = Material("BOPP MET", 910, "BOPP", "METALIZADO");
    private static readonly MaterialBobinaDto BoppAntifog = Material("BOPP ANTIFOG", 910, "BOPP", "ANTIFOG");
    private static readonly MaterialBobinaDto Pet = Material("PET", 1400, "PET");
    private static readonly MaterialBobinaDto Bopa = Material("BOPA", 2700, "BOPA") with { IsActive = false };
    private static readonly MaterialBobinaDto PeEstimado = Material("PE ESTIMADO", 920, "PE") with { EsEstimado = true };
    private static readonly MaterialBobinaDto PeCongelado = Material("PE ESTIMADO CONGELADO", 925, "PE") with { EsEstimado = true, FormulaLdpe = "CONGELADO" };
    private static readonly MaterialBobinaDto Ce03 = Material("CE-03", 918.1325, null);

    private readonly FakeBobinaStore _store = new(
        [Bopp, BoppMate, BoppMet, BoppAntifog, Pet, Bopa, PeEstimado, PeCongelado, Ce03],
        [new CoreBobinaDto(Guid.NewGuid(), "3\"", 0.095, 0.0188, true), new CoreBobinaDto(Guid.NewGuid(), "6\"", 0.1712, 0.02, true)]);

    private BobinaCalculatorService Service => new(_store);

    private static MaterialBobinaDto Material(string codigo, double densidad, string? tipo, string? terminado = null) =>
        new(Guid.NewGuid(), codigo, densidad, tipo, terminado, null, false, true, null, null);

    private static NpiCaptureDto Captura() => new()
    {
        TipoProducto = "BOBINA", Acabado = "IMPRESO", TipoImpresion = "EXTERNA (FRENTE)", Core = "3\"",
        PiTipoPelicula = "BOPP", TerminadoPI = "ANTIFOG", PiCalibre = "40", Ancho = "488", DiametroBobina = "22",
    };

    [Fact]
    public async Task Caso_pdf_et15595_desde_la_captura_convierte_mm_y_cm()
    {
        var r = await Service.CalcularCapturaAsync(new(null, Captura(), ModoCalculo.DesdeDiametro), default);

        r.G.Should().BeApproximately(0.0384, 1e-9);
        r.PesoNetoKg.Should().BeApproximately(14.49, 0.01);
        r.MetrosLineales.Should().BeApproximately(724, 1);
        r.Capas.Should().ContainSingle().Which.Should().Match<CapaCalculadaDto>(c => c.Codigo == "BOPP ANTIFOG" && c.Origen == OrigenDensidad.TipoTerminado);
        r.Avisos.Should().Contain(a => a.Contains("repetición"));
    }

    [Fact]
    public async Task Laminado_con_repeticion_en_mm_calcula_piezas()
    {
        var captura = Captura() with
        {
            TerminadoPI = "MATE", PiCalibre = "20", PplTipoPelicula = "BOPP", TerminadoPPL = "METALIZADO", PplCalibre = "20",
            Ancho = "160", Repeticion = "210",
        };

        var r = await Service.CalcularCapturaAsync(new(null, captura, ModoCalculo.DesdeDiametro), default);

        r.Laminaciones.Should().Be(1);
        r.PesoNetoKg.Should().BeApproximately(4.96, 0.01);
        r.PesoBrutoKg.Should().BeApproximately(5.26, 0.01);
        r.Piezas.Should().BeApproximately(3458, 1);
    }

    [Fact]
    public async Task Sin_impresion_no_suma_tinta_y_la_tarima_sale_de_la_captura()
    {
        var captura = Captura() with
        {
            Acabado = "Sin Impresión", PiTipoPelicula = "PET", TerminadoPI = "N/A", PiCalibre = "12", Ancho = "1140",
            PesoBobina = "60", DimTarimaBobina = "1.00 x 1.20", AlturaTarimaMaxBobina = "1.5",
        };

        var r = await Service.CalcularCapturaAsync(new(null, captura, ModoCalculo.DesdePeso), default);

        r.FactorTinta.Should().Be(0);
        r.DiametroCm.Should().BeApproximately(23.85, 0.01);
        r.Tarima.Total.Should().Be(20);
        r.Tarima.PesoKg.Should().BeApproximately(1242.86, 0.01);
    }

    [Fact]
    public async Task PE_usa_el_estimado_de_su_formula_y_avisa_que_falta_el_codigo_exacto()
    {
        var captura = Captura() with { PiTipoPelicula = "PE", TerminadoPI = "", FormulaLDPE_PI = "Congelado", PiCalibre = "50" };

        var r = await Service.CalcularCapturaAsync(new(null, captura, ModoCalculo.DesdeDiametro), default);

        r.Capas.Single().Should().Match<CapaCalculadaDto>(c => c.Codigo == "PE ESTIMADO CONGELADO" && c.Origen == OrigenDensidad.EstimadoPe);
        r.Avisos.Should().Contain(a => a.Contains("Ingeniería debe elegir"));
    }

    [Fact]
    public async Task PE_sin_formula_usa_el_estimado_general()
    {
        var captura = Captura() with { PiTipoPelicula = "PE", TerminadoPI = "", FormulaLDPE_PI = "N/A", PiCalibre = "50" };

        var r = await Service.CalcularCapturaAsync(new(null, captura, ModoCalculo.DesdeDiametro), default);

        r.Capas.Single().Codigo.Should().Be("PE ESTIMADO");
    }

    [Fact]
    public async Task El_codigo_exacto_de_ingenieria_gana_sobre_el_estimado()
    {
        var npiId = Guid.NewGuid();
        await _store.SetMaterialExactoAsync(npiId, "PI", Ce03.Id, new CurrentUser(Guid.NewGuid(), "UsuarioIngenieria", Domain.AreaType.Ingenieria), "Ing", default);
        var captura = Captura() with { PiTipoPelicula = "PE", TerminadoPI = "", FormulaLDPE_PI = "CONGELADO", PiCalibre = "50" };

        var r = await Service.CalcularCapturaAsync(new(npiId, captura, ModoCalculo.DesdeDiametro), default);

        r.Capas.Single().Should().Match<CapaCalculadaDto>(c => c.Codigo == "CE-03" && c.Origen == OrigenDensidad.Exacto);
        r.Avisos.Should().NotContain(a => a.Contains("Ingeniería debe elegir"));
    }

    [Fact]
    public async Task Un_terminado_sin_densidad_propia_usa_la_del_tipo_con_aviso()
    {
        var captura = Captura() with { TerminadoPI = "BLANCO" };

        var r = await Service.CalcularCapturaAsync(new(null, captura, ModoCalculo.DesdeDiametro), default);

        r.Capas.Single().Should().Match<CapaCalculadaDto>(c => c.Codigo == "BOPP" && c.Origen == OrigenDensidad.Tipo);
        r.Avisos.Should().Contain(a => a.Contains("BOPP BLANCO"));
    }

    [Fact]
    public async Task Un_material_inactivo_no_se_calcula_como_cero()
    {
        var captura = Captura() with { PiTipoPelicula = "BOPA", TerminadoPI = "" };

        var act = () => Service.CalcularCapturaAsync(new(null, captura, ModoCalculo.DesdeDiametro), default);

        (await act.Should().ThrowAsync<RequestValidationException>()).Which.Message.Should().Contain("PI: no hay densidad registrada para «BOPA»");
    }

    [Fact]
    public async Task Junta_todos_los_datos_faltantes_en_un_solo_mensaje()
    {
        var captura = new NpiCaptureDto { PplTipoPelicula = "BOPP" };

        var act = () => Service.CalcularCapturaAsync(new(null, captura, ModoCalculo.DesdePeso), default);

        var ex = (await act.Should().ThrowAsync<RequestValidationException>()).Which;
        ex.Message.Should().Contain("PPL: captura el calibre").And.Contain("Selecciona el core").And.Contain("ancho").And.Contain("peso por bobina");
    }

    [Fact]
    public async Task Calculadora_libre_con_materiales_del_catalogo()
    {
        var request = new CalculoLibreRequest([new CapaLibre(Pet.Id, 12)], 0, "3\"", 1140, ModoCalculo.DesdePeso, 60, 190, null, null, null);

        var r = await Service.CalcularLibreAsync(request, default);

        r.DiametroCm.Should().BeApproximately(23.85, 0.01);
        r.Piezas.Should().BeApproximately(15900, 1);
        r.Tarima.Total.Should().Be(20);
    }

    private sealed class FakeBobinaStore(List<MaterialBobinaDto> materiales, List<CoreBobinaDto> cores) : IBobinaCatalogStore
    {
        private readonly List<MaterialExactoDto> _exactos = [];

        public Task<CatalogoBobinaDto> GetAsync(CancellationToken cancellationToken) =>
            Task.FromResult(new CatalogoBobinaDto(materiales, cores, new ParametrosBobinaDto(1.7, 2.0, 100, 120, 150, 0)));

        public Task<IReadOnlyList<MaterialExactoDto>> GetMaterialesExactosAsync(Guid npiId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<MaterialExactoDto>>(_exactos);

        public Task SetMaterialExactoAsync(Guid npiId, string capa, Guid? materialId, CurrentUser actor, string actorName, CancellationToken cancellationToken)
        {
            _exactos.RemoveAll(e => e.Capa == capa);
            if (materialId is { } id) _exactos.Add(new MaterialExactoDto(capa, id, materiales.Single(m => m.Id == id).Codigo, actorName, DateTimeOffset.UtcNow));
            return Task.CompletedTask;
        }

        public Task<MaterialBobinaDto> SaveMaterialAsync(Guid? id, MaterialBobinaInput input, CurrentUser actor, string actorName, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task SaveCoreAsync(Guid id, double diametroExteriorM, double kgPorCm, bool isActive, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task SaveParametrosAsync(ParametrosBobinaDto parametros, CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
