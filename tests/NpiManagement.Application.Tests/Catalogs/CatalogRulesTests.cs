using FluentAssertions;
using NpiManagement.Application.Catalogs;
using NpiManagement.Domain.ValueObjects;
using Xunit;

namespace NpiManagement.Application.Tests.Catalogs;

public class CatalogRulesTests
{
    [Fact]
    public void Every_mapped_field_exists_in_the_capture()
    {
        var properties = typeof(NpiCaptureData).GetProperties().Select(p => p.Name).ToHashSet();

        CatalogRules.Fields.Values.SelectMany(f => f).Should().OnlyContain(f => properties.Contains(f));
    }

    [Theory]
    [InlineData("TIPO DE PRODUCTO", "BOBINA")]
    [InlineData("TIPO DE PRODUCTO", "pouch")]
    [InlineData("ACABADO", "Sin Impresión")]
    public void Options_used_by_form_rules_are_protected(string catalog, string value) =>
        CatalogRules.ProtectionReason(catalog, value).Should().NotBeNull();

    [Fact]
    public void Other_options_are_free_to_edit() =>
        CatalogRules.ProtectionReason("MERCADO", "SECUNDARIO").Should().BeNull();

    [Fact]
    public void Normalization_ignores_case_accents_and_extra_spaces() =>
        CatalogRules.Normalize("  Tipo   Impresión ").Should().Be(CatalogRules.Normalize("TIPO IMPRESION"));

    [Fact]
    public void Clean_keeps_the_text_but_trims_spaces() =>
        CatalogRules.Clean("  Bolsa   Stand Up ").Should().Be("Bolsa Stand Up");
}
