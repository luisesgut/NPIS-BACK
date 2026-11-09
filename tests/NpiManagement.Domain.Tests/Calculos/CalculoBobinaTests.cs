using FluentAssertions;
using NpiManagement.Domain.Calculos;
using NpiManagement.Domain.Exceptions;
using Xunit;

namespace NpiManagement.Domain.Tests.Calculos;

/// <summary>
/// Casos de la especificación "Cálculos de bobina: peso ↔ diámetro". Tolerancia ±0.01 en kg y cm,
/// ±1 en metros y piezas. Los comentarios "Excel" son lo que daba el archivo original.
/// </summary>
public class CalculoBobinaTests
{
    private static readonly CoreBobina Core3 = new("3\"", 0.095, 0.0188);

    private static EstructuraBobina Estructura(double tinta, params (string Material, double Densidad, double Micras)[] capas) =>
        new(capas.Select(c => new CapaBobina(c.Material, c.Densidad, c.Micras)).ToList(), tinta);

    [Fact]
    public void Caso1_pdf_et15595_diametro_a_peso()
    {
        var r = CalculoBobina.DesdeDiametro(Estructura(1, ("BOPP ANTIFOG", 910, 40)), Core3, 0.488, 0.22);

        r.G.Should().BeApproximately(0.0384, 1e-9);
        r.PesoNetoKg.Should().BeApproximately(14.49, 0.01);
        r.MetrosLineales.Should().BeApproximately(724, 1, "igual que el Excel: resta el core");
    }

    [Fact]
    public void Caso2_inverso_del_caso1_regresa_el_mismo_diametro()
    {
        var r = CalculoBobina.DesdePeso(Estructura(1, ("BOPP ANTIFOG", 910, 40)), Core3, 0.488, 14.49);

        (r.DiametroM * 100).Should().BeApproximately(22.00, 0.01);
    }

    [Fact]
    public void Caso3_laminado_diametro_a_peso_con_piezas()
    {
        var r = CalculoBobina.DesdeDiametro(Estructura(1, ("BOPP MATE", 910, 20), ("BOPP MET", 910, 20)), Core3, 0.16, 0.22, 0.21);

        r.G.Should().BeApproximately(0.0401, 1e-9);
        r.PesoNetoKg.Should().BeApproximately(4.96, 0.01);
        r.PesoBrutoKg.Should().BeApproximately(5.26, 0.01);
        r.MetrosLineales.Should().BeApproximately(726, 1, "Excel: 726");
        r.Piezas.Should().BeApproximately(3458, 1, "Excel: 3,458");
    }

    [Fact]
    public void Caso4_monocapa_peso_a_diametro_con_piezas()
    {
        var r = CalculoBobina.DesdePeso(Estructura(0, ("PET", 1400, 12)), Core3, 1.14, 60, 0.19);

        r.G.Should().BeApproximately(0.0168, 1e-9);
        (r.DiametroM * 100).Should().BeApproximately(23.85, 0.01);
        r.MetrosLineales.Should().BeApproximately(3021, 1, "Excel: 3,021");
        r.Piezas.Should().BeApproximately(15900, 1, "Excel: 15,900");
    }

    [Fact]
    public void Caso5_tarima_del_caso4_incluye_el_core_en_el_peso()
    {
        var bobina = CalculoBobina.DesdePeso(Estructura(0, ("PET", 1400, 12)), Core3, 1.14, 60);

        var t = CalculoTarima.Calcular(new TarimaBobinas(), bobina.DiametroM * 100, 114, bobina.PesoBrutoKg);

        t.PorCama.Should().Be(20);
        t.Camas.Should().Be(1);
        t.Total.Should().Be(20);
        t.PesoKg.Should().BeApproximately(1242.86, 0.01, "Excel: 1,200 kg sin core");
    }

    [Fact]
    public void Caso6_tarima_trunca_las_bobinas_que_no_caben()
    {
        var t = CalculoTarima.Calcular(new TarimaBobinas(), 22, 30, 0);

        t.PorCama.Should().Be(20, "Excel redondeaba 4.5 × 5.5 a 5 × 5 = 25");
        t.Camas.Should().Be(5);
        t.Total.Should().Be(100);
    }

    [Theory]
    [InlineData(60, 0, 2)]
    [InlineData(100, 0, 1)]
    [InlineData(40, 0, 3)]
    [InlineData(40, 0.10, 4)]
    public void Las_camas_no_rebasan_el_alto_maximo_salvo_tolerancia_explicita(double anchoCm, double tolerancia, int camas)
    {
        var t = CalculoTarima.Calcular(new TarimaBobinas(ToleranciaAlto: tolerancia), 22, anchoCm, 0);

        t.Camas.Should().Be(camas);
    }

    [Fact]
    public void Una_bobina_mas_ancha_que_el_alto_maximo_ocupa_una_cama()
    {
        CalculoTarima.Calcular(new TarimaBobinas(), 22, 160, 0).Camas.Should().Be(1);
    }

    [Fact]
    public void Sin_repeticion_no_se_calculan_piezas()
    {
        CalculoBobina.DesdePeso(Estructura(0, ("PET", 1400, 12)), Core3, 1.14, 60).Piezas.Should().BeNull();
        CalculoBobina.DesdePeso(Estructura(0, ("PET", 1400, 12)), Core3, 1.14, 60, 0).Piezas.Should().BeNull();
    }

    [Fact]
    public void Una_capa_sin_densidad_se_rechaza_en_vez_de_contar_como_cero()
    {
        var act = () => CalculoBobina.DesdePeso(Estructura(0, ("ME-10", 0, 20)), Core3, 1, 10);

        act.Should().Throw<NpiValidationException>().Which.Code.Should().Be("bobina-densidad-invalida");
    }

    [Theory]
    [InlineData(0.095)]
    [InlineData(0.05)]
    public void El_diametro_debe_ser_mayor_que_el_core(double diametroM)
    {
        var act = () => CalculoBobina.DesdeDiametro(Estructura(0, ("PET", 1400, 12)), Core3, 1, diametroM);

        act.Should().Throw<NpiValidationException>().Which.Code.Should().Be("bobina-diametro-menor-core");
    }

    [Fact]
    public void Ancho_cero_y_estructura_vacia_se_rechazan()
    {
        ((Action)(() => CalculoBobina.DesdePeso(Estructura(0, ("PET", 1400, 12)), Core3, 0, 10))).Should().Throw<NpiValidationException>().Which.Code.Should().Be("bobina-ancho-invalido");
        ((Action)(() => CalculoBobina.DesdePeso(Estructura(0), Core3, 1, 10))).Should().Throw<NpiValidationException>().Which.Code.Should().Be("bobina-sin-capas");
    }
}
