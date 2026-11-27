using FluentAssertions;
using NpiManagement.Application.Authorization;
using NpiManagement.Application.Npis.Commands;
using NpiManagement.Application.Npis.Dtos;
using NpiManagement.Application.Npis.SapOrders;
using NpiManagement.Application.Tests.Fakes;
using NpiManagement.Domain;
using NpiManagement.Domain.Common;
using NpiManagement.Domain.Exceptions;
using Xunit;

namespace NpiManagement.Application.Tests.Npis;

/// <summary>"Ponle orden de venta antes de crear otro NPI" (2026-11-27).</summary>
public class PendingSapOrdersTests
{
    // Lunes 12 de enero de 2026, 10:00 en México.
    private static readonly TestClock Clock = new(new DateTimeOffset(2026, 1, 12, 16, 0, 0, TimeSpan.Zero));
    private static readonly IBusinessCalendar Calendar = new MexicoBusinessCalendar();
    private static readonly Guid Seller = Guid.NewGuid();
    private static readonly Guid Director = Guid.NewGuid();

    private sealed class FakeReader(params PendingSapOrderRow[] rows) : IPendingSapOrderReader
    {
        public IReadOnlyCollection<Guid>? AskedFor { get; private set; }
        public Task<IReadOnlyList<PendingSapOrderRow>> ListAsync(IReadOnlyCollection<Guid> commercialUserIds, CancellationToken cancellationToken)
        {
            AskedFor = commercialUserIds;
            return Task.FromResult<IReadOnlyList<PendingSapOrderRow>>(rows.Where(r => commercialUserIds.Contains(r.AssignedCommercialUserId)).ToList());
        }
    }

    private static PendingSapOrderRow Row(Guid owner, string folio, DateTimeOffset ptAt, DateOnly? deferredUntil = null) =>
        new(Guid.NewGuid(), folio, "MASTRONARDI", "BOLSA", "PT-" + folio, null, ptAt, deferredUntil, deferredUntil is null ? null : "Sin pedido aún", owner, "Vendedor Uno");

    private static PendingSapOrdersService Service(FakeReader reader) => new(reader, Clock, Calendar);

    [Fact]
    public async Task Bloquea_solo_por_los_que_ya_pasaron_la_gracia_y_no_estan_pospuestos()
    {
        var reader = new FakeReader(
            Row(Seller, "NPI-0071", new DateTimeOffset(2026, 1, 5, 16, 0, 0, TimeSpan.Zero)),                 // PT hace una semana: bloquea
            Row(Seller, "NPI-0083", new DateTimeOffset(2026, 1, 9, 16, 0, 0, TimeSpan.Zero)),                 // viernes: bloquea desde el miércoles 14
            Row(Seller, "NPI-0090", new DateTimeOffset(2026, 1, 2, 16, 0, 0, TimeSpan.Zero), new DateOnly(2026, 1, 20))); // pospuesto

        var result = await Service(reader).GetAsync(new CurrentUser(Seller, ApplicationRoles.UsuarioComercial, AreaType.Comercial), CancellationToken.None);

        result.Blocked.Should().BeTrue();
        result.Items.Select(i => (i.NpiNumber, i.Blocking)).Should().Equal(("NPI-0071", true), ("NPI-0083", false), ("NPI-0090", false));
        result.Items[1].BlocksFrom.Should().Be(new DateOnly(2026, 1, 14));
        result.Items[2].DeferredUntil.Should().Be(new DateOnly(2026, 1, 20));
        result.Items.Should().OnlyContain(i => i.IsMine && i.OwnerName == null);
    }

    [Fact]
    public async Task Es_individual_al_director_solo_lo_bloquean_los_suyos_no_los_de_sus_vendedores()
    {
        var reader = new FakeReader(
            Row(Seller, "NPI-0071", new DateTimeOffset(2026, 1, 5, 16, 0, 0, TimeSpan.Zero)),
            Row(Director, "NPI-0072", new DateTimeOffset(2026, 1, 9, 16, 0, 0, TimeSpan.Zero)));
        var director = new CurrentUser(Director, ApplicationRoles.AdminComercial, AreaType.Comercial);

        var result = await Service(reader).GetAsync(director, CancellationToken.None);

        reader.AskedFor.Should().BeEquivalentTo([Director]);
        result.Blocked.Should().BeFalse("el vencido es de su vendedor; el suyo sigue en gracia");
        result.Items.Select(i => i.NpiNumber).Should().Equal("NPI-0072");
    }

    [Fact]
    public async Task AdminTotal_y_otras_areas_nunca_quedan_bloqueados()
    {
        var reader = new FakeReader(Row(Seller, "NPI-0071", new DateTimeOffset(2026, 1, 5, 16, 0, 0, TimeSpan.Zero)));
        var service = Service(reader);

        (await service.GetAsync(new CurrentUser(Seller, ApplicationRoles.AdminTotal, AreaType.Total), CancellationToken.None)).Blocked.Should().BeFalse();
        (await service.GetAsync(new CurrentUser(Seller, ApplicationRoles.UsuarioIngenieria, AreaType.Ingenieria), CancellationToken.None)).Items.Should().BeEmpty();
        reader.AskedFor.Should().BeNull();
    }

    [Fact]
    public async Task Crear_NPI_falla_mientras_haya_ordenes_vencidas()
    {
        var reader = new FakeReader(Row(Seller, "NPI-0071", new DateTimeOffset(2026, 1, 5, 16, 0, 0, TimeSpan.Zero)));
        var handler = new CreateNpiDraftCommandHandler(new FakeNpiRepository(), new FakeUnitOfWork(), Clock, new FakeUserDirectory(), Service(reader));
        var user = new CurrentUser(Seller, ApplicationRoles.UsuarioComercial, AreaType.Comercial);

        var act = () => handler.HandleAsync(new CreateNpiDraftCommand(Seller, user.Role, "Vendedor Uno", new NpiCaptureDto(), Priority.Media, null, Viewer: user), CancellationToken.None);

        (await act.Should().ThrowAsync<InvalidNpiTransitionException>()).Which.Code.Should().Be("sap-orders-pending");
    }

    [Fact]
    public async Task Crear_NPI_funciona_si_lo_pendiente_sigue_en_gracia()
    {
        var reader = new FakeReader(Row(Seller, "NPI-0083", new DateTimeOffset(2026, 1, 9, 16, 0, 0, TimeSpan.Zero)));
        var handler = new CreateNpiDraftCommandHandler(new FakeNpiRepository(), new FakeUnitOfWork(), Clock, new FakeUserDirectory(), Service(reader));
        var user = new CurrentUser(Seller, ApplicationRoles.UsuarioComercial, AreaType.Comercial);

        var id = await handler.HandleAsync(new CreateNpiDraftCommand(Seller, user.Role, "Vendedor Uno", new NpiCaptureDto(), Priority.Media, null, Viewer: user), CancellationToken.None);

        id.Should().NotBeEmpty();
    }
}
