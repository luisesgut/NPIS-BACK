using FluentAssertions;
using NpiManagement.Application.Authorization;
using NpiManagement.Application.Exceptions;
using NpiManagement.Application.Users;
using NpiManagement.Domain;
using Xunit;

namespace NpiManagement.Application.Tests;

public class ClientViewGrantsTests
{
    private static readonly Guid Owner = Guid.NewGuid();
    private static readonly Guid Ana = Guid.NewGuid();
    private static readonly NpiOwnership Ownership = new(Owner, Owner, null, null);

    private static CurrentUser AnaWith(params string[] clients) =>
        new(Ana, ApplicationRoles.UsuarioComercial, AreaType.Comercial, clients.Length == 0 ? null : clients.ToHashSet(StringComparer.OrdinalIgnoreCase));

    [Fact]
    public void A_shared_client_lets_the_user_see_but_not_operate()
    {
        var ana = AnaWith("ALIMENTOS X");

        NpiAuthorization.CanView(ana, Ownership, "Alimentos  x ", LifecycleStatus.Active).Should().BeTrue();
        NpiAuthorization.IsViewOnly(ana, Ownership, "ALIMENTOS X", LifecycleStatus.Completed).Should().BeTrue();
        // El borrador de otro vendedor todavía no se comparte.
        NpiAuthorization.CanView(ana, Ownership, "ALIMENTOS X", LifecycleStatus.Draft).Should().BeFalse();
        // Operar (acciones, chat, adjuntos) sigue exigiendo ser dueño o responsable.
        NpiAuthorization.CanAccess(ana, Ownership).Should().BeFalse();
        NpiAuthorization.CanActAsCommercial(ana, Owner, Owner).Should().BeFalse();
    }

    [Fact]
    public void Without_the_client_shared_nothing_changes()
    {
        NpiAuthorization.CanView(AnaWith("OTRO CLIENTE"), Ownership, "ALIMENTOS X", LifecycleStatus.Active).Should().BeFalse();
        NpiAuthorization.CanView(AnaWith(), Ownership, "ALIMENTOS X", LifecycleStatus.Active).Should().BeFalse();
    }

    [Fact]
    public void Own_npis_are_never_marked_view_only()
    {
        var owner = new CurrentUser(Owner, ApplicationRoles.UsuarioComercial, AreaType.Comercial, new HashSet<string> { "ALIMENTOS X" });
        NpiAuthorization.IsViewOnly(owner, Ownership, "ALIMENTOS X", LifecycleStatus.Active).Should().BeFalse();
    }

    [Fact]
    public void Clients_are_normalized_deduplicated_and_capped()
    {
        ClientViewGrants.Clean([" alimentos  x", "ALIMENTOS X", "", "bebidas"]).Should().Equal("ALIMENTOS X", "BEBIDAS");
        var tooMany = Enumerable.Range(0, ClientViewGrants.MaxClients + 1).Select(i => $"CLIENTE {i}");
        FluentActions.Invoking(() => ClientViewGrants.Clean(tooMany)).Should().Throw<RequestValidationException>();
    }

    [Fact]
    public void Ver_y_trabajar_lets_the_user_operate_as_the_team_of_their_area()
    {
        var clock = TestClock.Default();
        var seller = new NpiManagement.Domain.Common.Actor(Owner, "UsuarioComercial", "Vendedor");
        var npi = Npi.CreateDraft(Guid.NewGuid(), seller, clock, NpiManagement.Domain.ValueObjects.NpiCaptureData.Empty with { Cliente = "ALIMENTOS X" }, Priority.Media);
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "ALIMENTOS X" };
        var ana = new CurrentUser(Ana, ApplicationRoles.UsuarioComercial, AreaType.Comercial, set, set);

        // Borrador: todavía es sólo del vendedor.
        NpiAuthorization.CanActAsCommercial(ana, npi).Should().BeFalse();
        NpiAuthorization.BuildAccessPredicate(ana).Compile()(npi).Should().BeFalse();

        npi.Submit(seller, clock, new NpiManagement.Domain.Common.MexicoBusinessCalendar(), NpiManagement.Domain.ValueObjects.NpiNumber.FromSequenceValue(7));
        NpiAuthorization.CanActAsCommercial(ana, npi).Should().BeTrue();
        NpiAuthorization.CanAccess(ana, NpiAuthorization.OwnershipOf(npi)).Should().BeTrue();
        NpiAuthorization.BuildAccessPredicate(ana).Compile()(npi).Should().BeTrue();
        NpiAuthorization.IsViewOnly(ana, NpiAuthorization.OwnershipOf(npi), npi.Capture.Cliente, npi.LifecycleStatus).Should().BeFalse();
        NpiAuthorization.WorksOnlyByClient(ana, npi).Should().BeTrue();
        // Sólo como su área, y no invita a nadie al equipo.
        NpiAuthorization.CanActAsDesign(ana, npi).Should().BeFalse();
        NpiAuthorization.CanManageParticipants(ana, npi, AreaType.Comercial).Should().BeFalse();

        // Sólo ver: sigue siendo consulta.
        var viewer = new CurrentUser(Ana, ApplicationRoles.UsuarioComercial, AreaType.Comercial, set);
        NpiAuthorization.CanActAsCommercial(viewer, npi).Should().BeFalse();
        NpiAuthorization.IsViewOnly(viewer, NpiAuthorization.OwnershipOf(npi), npi.Capture.Cliente, npi.LifecycleStatus).Should().BeTrue();
    }

    [Fact]
    public void Work_only_counts_for_clients_that_are_also_shared()
    {
        ClientViewGrants.CleanGrants(["alimentos x", "bebidas"], ["Alimentos  X", "OTRO"])
            .Should().Equal(new ClientViewGrantDto("ALIMENTOS X", true), new ClientViewGrantDto("BEBIDAS", false));
    }
}
