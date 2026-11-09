using System.Text.Json;
using FluentAssertions;
using NpiManagement.Application.Abstractions;
using NpiManagement.Infrastructure.Sispro;
using Xunit;

namespace NpiManagement.IntegrationTests.Sispro;

/// <summary>Traducción de cotizaciones reales de SISPRO a la captura del NPI (sin red ni base de datos).</summary>
public class SisproQuotationMapperTests
{
    private static readonly IReadOnlyDictionary<string, SisproEquivalence> NoEquivalences = new Dictionary<string, SisproEquivalence>();

    private static SisproCotizacionDto Parse(string json) =>
        JsonSerializer.Deserialize<SisproCotizacionDto>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;

    private static QuotationSnapshot Map(string json, IReadOnlyDictionary<string, SisproEquivalence>? equivalences = null,
        IReadOnlyDictionary<string, IReadOnlyList<string>>? catalogs = null) =>
        SisproQuotationMapper.Map(Parse(json), equivalences ?? NoEquivalences, catalogs ?? SisproFixtures.Catalogs);

    [Fact]
    public void A_laminated_pouch_fills_the_structure_the_product_and_the_closure()
    {
        var q = Map(SisproFixtures.Cotizacion678);

        q.NumeroCotizacion.Should().Be("678");
        q.Estado.Should().Be("Cotizada");
        q.Campos.Should().Contain(new Dictionary<string, string>
        {
            ["numeroCotizacion"] = "678",
            ["piTipoPelicula"] = "PET",
            ["piCalibre"] = "12",
            ["terminadoPI"] = "TRANSPARENTE",
            ["pplTipoPelicula"] = "PE",
            ["pplCalibre"] = "75",
            ["formulaLDPE_PPL"] = "LAMINACIÓN",
            ["tipoProducto"] = "POUCH",
            ["formato"] = "POUCH 2 SELLOS",
            ["cierre"] = "ZIPPER",
            ["tipoImpresion"] = "INTERNA (REVERSO)",
            ["unidadVenta"] = "MILL",
        });
        q.Campos.Should().NotContainKey("pslTipoPelicula");
        q.Campos.Should().NotContainKeys(["ancho", "alto"], "SISPRO guarda la imposición, no la medida de la bolsa");
        q.Campos.Should().NotContainKey("cliente", "en SISPRO viene como «Cliente» genérico");
    }

    [Fact]
    public void Rule_based_translations_are_marked_for_review_and_the_reference_data_is_kept()
    {
        var q = Map(SisproFixtures.Cotizacion678);

        q.CamposInferidos.Should().Contain(["piTipoPelicula", "pplTipoPelicula", "formulaLDPE_PPL", "tipoProducto", "formato", "descripcion"]);
        q.CamposInferidos.Should().NotContain(["piCalibre", "unidadVenta", "numeroCotizacion"]);
        q.Capas.Select(c => (c.Capa, c.MaterialSispro, c.Origen)).Should().Equal(
            ("PI", "PET NAT", "inferido"), ("PPL", "LDPE RESPALDO LAM / GENERICO", "inferido"));
        q.Imposicion.Should().Be(new QuotationImposition(764, 244, 1, 2, 794));
        (q.ColoresInterno, q.ColoresExterno).Should().Be((8, 0));
        q.Cantidades.Select(c => (c.Cantidad, c.PrecioUnidadVenta)).Should().Equal((50m, 2742.327m), (100m, 2566.368m));
        q.Advertencias.Should().Contain(a => a.Contains("cliente"));
        q.Advertencias.Should().Contain(a => a.Contains("Ancho y alto"));
    }

    [Fact]
    public void A_printed_laminated_coil_gets_its_core_and_plug()
    {
        var q = Map(SisproFixtures.Cotizacion677);

        q.Campos["tipoProducto"].Should().Be("BOBINA");
        q.Campos["core"].Should().Be("3\"");
        q.Campos["taponCore"].Should().Be("SI");
        q.Campos["pplCalibre"].Should().Be("65");
        q.Campos["unidadVenta"].Should().Be("KG");
        q.Campos["tipoImpresion"].Should().Be("INTERNA (REVERSO)");
    }

    [Fact]
    public void A_value_missing_from_the_catalog_is_not_filled_and_is_explained()
    {
        // "BOBINA LAMINADA" no está en el catálogo FORMATO de prueba (como pasa en producción si lo dieron de baja).
        var q = Map(SisproFixtures.Cotizacion677);

        q.Campos.Should().NotContainKey("formato");
        q.Advertencias.Should().Contain(a => a.StartsWith("Formato") && a.Contains("BOBINA LAMINADA"));
    }

    [Fact]
    public void A_film_without_thickness_is_translated_but_the_calibre_is_left_for_the_seller()
    {
        var q = Map(SisproFixtures.Cotizacion680);

        q.Campos["piTipoPelicula"].Should().Be("PE");
        q.Campos["formulaLDPE_PI"].Should().Be("COMERCIAL");
        q.Campos.Should().NotContainKey("piCalibre");
        q.Advertencias.Should().Contain(a => a.Contains("no trae el calibre") && a.Contains("LDPE COMERCIAL NAT"));
        q.Campos["formato"].Should().Be("BOBINA PLANA");
        q.Campos.Should().NotContainKey("tipoImpresion", "no lleva tintas");
    }

    [Fact]
    public void An_equivalence_from_engineering_wins_over_the_name_rules()
    {
        var equivalences = new Dictionary<string, SisproEquivalence>
        {
            [SisproQuotationMapper.Normalize("ldpe respaldo lam / genérico")] = new("PE", "BAJO COF", "LAMINACIÓN"),
        };

        var q = Map(SisproFixtures.Cotizacion678, equivalences);

        q.Campos["terminadoPPL"].Should().Be("BAJO COF");
        q.CamposInferidos.Should().NotContain(["pplTipoPelicula", "terminadoPPL", "formulaLDPE_PPL"]);
        q.Capas.Single(c => c.Capa == "PPL").Origen.Should().Be("equivalencia");
    }

    [Fact]
    public void An_unknown_material_is_left_empty_with_a_warning()
    {
        var q = Map(SisproFixtures.Cotizacion678.Replace("\"PET NAT\"", "\"PVDC 20\""));

        q.Campos.Should().NotContainKey("piTipoPelicula");
        q.Capas.Single(c => c.Capa == "PI").Origen.Should().Be("sin-equivalencia");
        q.Advertencias.Should().Contain(a => a.Contains("PVDC 20") && a.Contains("Equivalencias SISPRO"));
    }

    [Fact]
    public void A_quote_without_a_price_yet_is_flagged()
    {
        var q = Map(SisproFixtures.Cotizacion678.Replace("\"Cotizada\"", "\"A cotizar\""));

        SisproQuotationMapper.IsQuoted(q.Estado).Should().BeFalse();
        q.Advertencias.Should().Contain(a => a.Contains("todavía no tiene precio"));
    }

    // Referencias reales del listado de SISPRO (2026-10-27). Usuarios del sistema: los vendedores.
    private static readonly string[] Vendedores = ["Luis Vega", "Daniel Navarro", "Sandra Gómez"];

    [Theory]
    [InlineData("PROAN - BOLSA POUCH ROYAL RACE 5 PATITAS DE POLLO CON ZIPPER", "PROAN", "BOLSA POUCH ROYAL RACE 5 PATITAS DE POLLO CON ZIPPER")]
    [InlineData("COVEMEX - BOB MONOCAPA LDPEBCO - HEB ZUCCHINI SLICED 454 gr", "COVEMEX", "BOB MONOCAPA LDPEBCO - HEB ZUCCHINI SLICED 454 gr")]
    [InlineData("LUIS VEGA - CRESTOM HILL - BOLSA POUCH 2 SELLOS - CAMARON MEDIANO 907 gr", "CRESTOM HILL", "BOLSA POUCH 2 SELLOS - CAMARON MEDIANO 907 gr")]
    [InlineData("DAN NAVARRO - STAR DEL NORTE - BOB LAMINADA IMPRESA 5T VARIOS", "STAR DEL NORTE", "BOB LAMINADA IMPRESA 5T VARIOS")]
    [InlineData("EMBAG - MARIA ZARATE - BOB MONOCAPA LDPE - APROZA POP CORN 450 gr", "EMBAG", "BOB MONOCAPA LDPE - APROZA POP CORN 450 gr")]
    [InlineData("ROLLSTOCK DESTINY DOLE", null, "ROLLSTOCK DESTINY DOLE")]
    [InlineData("BOLSA POUCH - CAFE 1 KG", null, "BOLSA POUCH - CAFE 1 KG")]
    public void The_client_is_taken_from_the_reference_skipping_the_seller(string referencia, string? cliente, string descripcion)
    {
        SisproQuotationMapper.SplitReferencia(referencia, Vendedores).Should().Be((cliente, descripcion));
    }

    [Fact]
    public void A_client_found_in_the_reference_is_filled_for_review()
    {
        var q = SisproQuotationMapper.Map(Parse(SisproFixtures.Cotizacion678.Replace("\"REF-678\"", "\"PROAN - BOLSA POUCH ROYAL RACE CON ZIPPER\"")),
            NoEquivalences, SisproFixtures.Catalogs, Vendedores);

        q.Campos["cliente"].Should().Be("PROAN");
        q.Campos["descripcion"].Should().Be("BOLSA POUCH ROYAL RACE CON ZIPPER");
        q.CamposInferidos.Should().Contain(["cliente", "descripcion"]);
        q.Advertencias.Should().Contain(a => a.Contains("se tomó de la referencia") && a.Contains("PROAN"));
    }

    [Theory]
    [InlineData("  ldpe   Respaldo  ", "LDPE RESPALDO")]
    [InlineData("Polímero", "POLIMERO")]
    [InlineData("LAMINACIÓN", "LAMINACION")]
    public void Names_are_compared_without_case_accents_or_extra_spaces(string raw, string expected)
    {
        SisproQuotationMapper.Normalize(raw).Should().Be(expected);
    }

    [Fact]
    public void The_snapshot_survives_a_json_round_trip_as_stored_in_the_npi()
    {
        var q = Map(SisproFixtures.Cotizacion678);
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);

        var back = JsonSerializer.Deserialize<QuotationSnapshot>(JsonSerializer.Serialize(q, options), options)!;

        back.Campos.Should().BeEquivalentTo(q.Campos);
        back.Capas.Should().Equal(q.Capas);
        back.Cantidades.Should().Equal(q.Cantidades);
    }
}
