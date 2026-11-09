using FluentAssertions;
using NpiManagement.Application.Abstractions;
using NpiManagement.Application.Authorization;
using NpiManagement.Application.Exceptions;
using NpiManagement.Application.Npis;
using NpiManagement.Application.Npis.Commands;
using NpiManagement.Application.Npis.Dtos;
using NpiManagement.Application.Tests.Fakes;
using NpiManagement.Domain;
using NpiManagement.Domain.Common;
using NpiManagement.Domain.ValueObjects;
using Xunit;

namespace NpiManagement.Application.Tests.Npis;

public class SisproQuoteTests
{
    private static QuotationSnapshot Quote(params QuotationLayer[] capas) =>
        new("678", "Cotizada", "REF", null, "Millar", new Dictionary<string, string> { ["formato"] = "POUCH 2 SELLOS" }, [], capas, null, 8, 0,
            [new QuotationQuantity(50, 2742.33m, 137116.16m)], []);

    private static QuotationLayer Layer(string capa, string material, decimal? espesor, string tipo) => new(capa, material, espesor, tipo, "", "", "inferido");

    private static readonly NpiCaptureData PetLdpe = NpiCaptureData.Empty with
    {
        PiTipoPelicula = "PET", PiCalibre = "12", PplTipoPelicula = "PE", PplCalibre = "75", Formato = "POUCH 2 SELLOS",
    };

    // ===== Comparación de estructura =====

    [Fact]
    public void The_same_structure_has_no_differences()
    {
        var warnings = SisproStructureComparer.Compare(Quote(Layer("PI", "PET NAT", 12, "PET"), Layer("PPL", "LDPE RESPALDO", 75, "PE")), PetLdpe);

        warnings.Should().BeEmpty();
    }

    [Fact]
    public void A_different_calibre_or_film_is_reported_per_layer()
    {
        var warnings = SisproStructureComparer.Compare(Quote(Layer("PI", "BOPP NAT", 12, "BOPP"), Layer("PPL", "LDPE RESPALDO", 62.5m, "PE")), PetLdpe);

        warnings.Should().HaveCount(2);
        warnings.Should().Contain(w => w.Contains("Película de impresión") && w.Contains("BOPP") && w.Contains("PET"));
        warnings.Should().Contain(w => w.Contains("1ª laminación") && w.Contains("62.5") && w.Contains("75"));
    }

    [Fact]
    public void Rounding_in_the_calibre_is_not_a_difference()
    {
        var warnings = SisproStructureComparer.Compare(Quote(Layer("PI", "PET NAT", 12.5m, "PET"), Layer("PPL", "LDPE", 75, "PE")), PetLdpe);

        warnings.Should().BeEmpty();
    }

    [Fact]
    public void Missing_or_extra_layers_and_untranslated_materials_are_reported()
    {
        var missingLamination = SisproStructureComparer.Compare(Quote(Layer("PI", "PET NAT", 12, "PET")), PetLdpe);
        missingLamination.Should().ContainSingle(w => w.Contains("1ª laminación") && w.Contains("no trae esa capa"));

        var untranslated = SisproStructureComparer.Compare(Quote(Layer("PI", "PVDC 20", 20, ""), Layer("PPL", "LDPE", 75, "PE")), PetLdpe);
        untranslated.Should().ContainSingle(w => w.Contains("no se pudo traducir") && w.Contains("PVDC 20"));

        var extra = SisproStructureComparer.Compare(Quote(Layer("PI", "PET NAT", 12, "PET"), Layer("PPL", "LDPE", 75, "PE"), Layer("PSL", "CPP", 20, "CPP")), PetLdpe);
        extra.Should().ContainSingle(w => w.Contains("2ª laminación") && w.Contains("el NPI no lleva esa capa"));
    }

    [Fact]
    public void A_different_format_is_reported()
    {
        var warnings = SisproStructureComparer.Compare(Quote(Layer("PI", "PET NAT", 12, "PET"), Layer("PPL", "LDPE", 75, "PE")), PetLdpe with { Formato = "POUCH 3 SELLOS" });

        warnings.Should().ContainSingle(w => w.StartsWith("Formato"));
    }

    // ===== Precios sólo para Comercial =====

    [Theory]
    [InlineData(ApplicationRoles.AdminTotal, true)]
    [InlineData(ApplicationRoles.AdminComercial, true)]
    [InlineData(ApplicationRoles.UsuarioComercial, true)]
    [InlineData(ApplicationRoles.AdminIngenieria, false)]
    [InlineData(ApplicationRoles.UsuarioIngenieria, false)]
    [InlineData(ApplicationRoles.AdminDiseno, false)]
    [InlineData(ApplicationRoles.UsuarioDiseno, false)]
    public void Only_commercial_and_admin_total_see_quote_prices(string role, bool sees)
    {
        QuotePriceRedaction.CanSeePrices(role).Should().Be(sees);
    }

    [Fact]
    public void Redaction_removes_prices_but_keeps_quantities()
    {
        var redacted = QuotePriceRedaction.Redact(Quote())!;

        redacted.Cantidades.Should().ContainSingle().Which.Should().Be(new QuotationQuantity(50, null, null));
    }

    // ===== Comandos =====

    private static readonly Guid CommercialId = Guid.NewGuid();
    private static readonly Guid EngineerId = Guid.NewGuid();
    private static readonly CurrentUser Commercial = new(CommercialId, "UsuarioComercial", AreaType.Comercial);

    /// <summary>NPI con la factibilidad de Ingeniería aprobada y la captura PET 12 / PE 75, listo para ligar cotización.</summary>
    private static (FakeNpiRepository Repo, Guid NpiId) FeasibleNpi(TestClock clock)
    {
        var repo = new FakeNpiRepository();
        var calendar = new MexicoBusinessCalendar();
        var commercial = new Actor(CommercialId, "UsuarioComercial", "Comercial Uno");
        var engineer = new Actor(EngineerId, "UsuarioIngenieria", "Ingeniero Uno");
        var npi = NpiManagement.Domain.Npi.CreateDraft(Guid.NewGuid(), commercial, clock, PetLdpe, Priority.Media, EngineerId);
        npi.Submit(commercial, clock, calendar, NpiNumber.FromSequenceValue(1));
        npi.ApproveEngineeringFeasibility(engineer, clock, calendar, null);
        repo.Add(npi);
        return (repo, npi.Id);
    }

    [Fact]
    public async Task Linking_compares_the_structure_and_tells_engineering_when_it_differs()
    {
        var clock = TestClock.Default();
        var (repo, npiId) = FeasibleNpi(clock);
        var quotations = new FakeQuotationProvider().With("678", capas: [Layer("PI", "PET NAT", 12, "PET"), Layer("PPL", "LDPE", 62.5m, "PE")]);
        var notifications = new FakeNotificationService();

        var detail = await new LinkSisproQuoteCommandHandler(repo, new FakeUnitOfWork(), clock, new MexicoBusinessCalendar(), quotations, notifications)
            .HandleAsync(new LinkSisproQuoteCommand(npiId, Commercial, "Comercial Uno", "678"), CancellationToken.None);

        var quote = detail.SisproQuotes.Should().ContainSingle().Subject;
        quote.StructureWarnings.Should().ContainSingle(w => w.Contains("62.5"));
        quote.Snapshot!.Cantidades.Should().ContainSingle().Which.PrecioUnidadVenta.Should().Be(2742.33m);
        notifications.Sent.Should().ContainSingle(n => n.RecipientUserId == EngineerId && n.Message.Contains("no coincide"));
    }

    [Fact]
    public async Task A_quote_that_does_not_exist_or_has_no_price_yet_cannot_be_linked()
    {
        var clock = TestClock.Default();
        var (repo, npiId) = FeasibleNpi(clock);
        var quotations = new FakeQuotationProvider().With("700", estado: "A cotizar");
        var handler = new LinkSisproQuoteCommandHandler(repo, new FakeUnitOfWork(), clock, new MexicoBusinessCalendar(), quotations, new FakeNotificationService());

        var missing = () => handler.HandleAsync(new LinkSisproQuoteCommand(npiId, Commercial, "Comercial Uno", "999"), CancellationToken.None);
        (await missing.Should().ThrowAsync<RequestValidationException>()).Which.Code.Should().Be("sispro-quote-not-found");

        var notQuoted = () => handler.HandleAsync(new LinkSisproQuoteCommand(npiId, Commercial, "Comercial Uno", "700"), CancellationToken.None);
        (await notQuoted.Should().ThrowAsync<RequestValidationException>()).Which.Code.Should().Be("sispro-quote-not-quoted");
    }

    [Fact]
    public async Task Another_commercial_user_cannot_link_a_quote_to_a_npi_that_is_not_theirs()
    {
        var clock = TestClock.Default();
        var (repo, npiId) = FeasibleNpi(clock);
        var stranger = new CurrentUser(Guid.NewGuid(), "UsuarioComercial", AreaType.Comercial);
        var handler = new LinkSisproQuoteCommandHandler(repo, new FakeUnitOfWork(), clock, new MexicoBusinessCalendar(), new FakeQuotationProvider().With("678"), new FakeNotificationService());

        var act = () => handler.HandleAsync(new LinkSisproQuoteCommand(npiId, stranger, "Otro", "678"), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task The_saved_snapshot_is_read_back_in_the_detail()
    {
        var clock = TestClock.Default();
        var (repo, npiId) = FeasibleNpi(clock);
        await new LinkSisproQuoteCommandHandler(repo, new FakeUnitOfWork(), clock, new MexicoBusinessCalendar(), new FakeQuotationProvider().With("678"), new FakeNotificationService())
            .HandleAsync(new LinkSisproQuoteCommand(npiId, Commercial, "Comercial Uno", "678"), CancellationToken.None);

        NpiDetailDto detail = NpiMapper.ToDetail((await repo.GetByIdAsync(npiId, CancellationToken.None))!, clock, new MexicoBusinessCalendar(), []);

        detail.SisproQuotes.Single().Snapshot!.UnidadVenta.Should().Be("Millar");
    }
}
