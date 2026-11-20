using FluentAssertions;
using NpiManagement.Application.Authorization;
using NpiManagement.Domain;
using NpiManagement.Domain.Common;
using NpiManagement.Domain.ValueObjects;
using Xunit;

namespace NpiManagement.Application.Tests;

/// <summary>
/// "Mi equipo" de Comercial (2026-11-18): el vendedor trabaja los NPIs de su director (Admin Comercial,
/// que ya ve todo) como si fueran suyos; los vendedores del mismo director no se ven entre ellos.
/// </summary>
public class CommercialDirectorTests
{
    private static readonly Guid Hugo = Guid.NewGuid();
    private static readonly Guid Dayana = Guid.NewGuid();
    private static readonly Guid Omar = Guid.NewGuid();

    private static CurrentUser Seller(Guid id, Guid? director = null) => new(id, ApplicationRoles.UsuarioComercial, AreaType.Comercial, DirectorUserId: director ?? Hugo);

    private static Npi NpiOf(Guid owner, bool submit = true)
    {
        var clock = TestClock.Default();
        var actor = new Actor(owner, "UsuarioComercial", "Dueño");
        var npi = Npi.CreateDraft(Guid.NewGuid(), actor, clock, NpiCaptureData.Empty with { Cliente = "MR. LUCKY" }, Priority.Media);
        if (submit) npi.Submit(actor, clock, new MexicoBusinessCalendar(), NpiNumber.FromSequenceValue(46));
        return npi;
    }

    [Fact]
    public void The_seller_works_the_npis_of_their_director()
    {
        var dayana = Seller(Dayana);
        var hugos = NpiOf(Hugo);

        NpiAuthorization.CanAccess(dayana, NpiAuthorization.OwnershipOf(hugos)).Should().BeTrue();
        NpiAuthorization.CanActAsCommercial(dayana, hugos).Should().BeTrue();
        NpiAuthorization.WorksOnlyByClient(dayana, hugos).Should().BeTrue("el front lo trata como equipo");
        NpiAuthorization.BuildAccessPredicate(dayana).Compile()(hugos).Should().BeTrue();
        NpiAuthorization.BuildViewPredicate(dayana).Compile()(hugos).Should().BeTrue();
        // Sólo como Comercial.
        NpiAuthorization.CanActAsDesign(dayana, hugos).Should().BeFalse();
    }

    [Fact]
    public void Sellers_of_the_same_director_do_not_see_each_other()
    {
        var omar = Seller(Omar);
        var dayanas = NpiOf(Dayana);

        NpiAuthorization.CanAccess(omar, NpiAuthorization.OwnershipOf(dayanas)).Should().BeFalse();
        NpiAuthorization.CanActAsCommercial(omar, dayanas).Should().BeFalse();
        NpiAuthorization.BuildAccessPredicate(omar).Compile()(dayanas).Should().BeFalse();
    }

    [Fact]
    public void The_directors_drafts_are_still_only_his()
    {
        var dayana = Seller(Dayana);
        var draft = NpiOf(Hugo, submit: false);

        NpiAuthorization.CanAccess(dayana, NpiAuthorization.OwnershipOf(draft)).Should().BeFalse();
        NpiAuthorization.CanActAsCommercial(dayana, draft).Should().BeFalse();
    }

    [Fact]
    public void Turns_and_dashboard_leave_the_directors_npis_out()
    {
        var dayana = Seller(Dayana);

        NpiAuthorization.BuildAccessPredicate(dayana, includeDirector: false).Compile()(NpiOf(Hugo)).Should().BeFalse();
        NpiAuthorization.BuildAccessPredicate(dayana, includeDirector: false).Compile()(NpiOf(Dayana)).Should().BeTrue();
    }

    [Fact]
    public void Without_director_nothing_changes()
    {
        var alone = new CurrentUser(Dayana, ApplicationRoles.UsuarioComercial, AreaType.Comercial);
        NpiAuthorization.CanAccess(alone, NpiAuthorization.OwnershipOf(NpiOf(Hugo))).Should().BeFalse();
    }

    [Fact]
    public async Task The_seller_can_start_a_new_npi_from_one_of_their_director_and_the_link_is_kept()
    {
        // Antes la liga "Basado en" se armaba sin el director y se perdía sin avisar (2026-11-20).
        var repository = new Fakes.FakeNpiRepository();
        var hugos = NpiOf(Hugo);
        var omars = NpiOf(Omar);
        repository.Add(hugos);
        repository.Add(omars);
        var handler = new NpiManagement.Application.Npis.Commands.CreateNpiDraftCommandHandler(repository, new Fakes.FakeUnitOfWork(), TestClock.Default(), new Fakes.FakeUserDirectory());
        var dayana = Seller(Dayana);

        var fromDirector = await handler.HandleAsync(new NpiManagement.Application.Npis.Commands.CreateNpiDraftCommand(Dayana, dayana.Role, "Dayana", new NpiManagement.Application.Npis.Dtos.NpiCaptureDto(), Priority.Media, null, hugos.Id, dayana), CancellationToken.None);
        var fromPeer = await handler.HandleAsync(new NpiManagement.Application.Npis.Commands.CreateNpiDraftCommand(Dayana, dayana.Role, "Dayana", new NpiManagement.Application.Npis.Dtos.NpiCaptureDto(), Priority.Media, null, omars.Id, dayana), CancellationToken.None);

        (await repository.GetByIdAsync(fromDirector, CancellationToken.None))!.BasedOnNpiId.Should().Be(hugos.Id);
        (await repository.GetByIdAsync(fromPeer, CancellationToken.None))!.BasedOnNpiId.Should().BeNull("los vendedores no se ven entre ellos");
    }
}
