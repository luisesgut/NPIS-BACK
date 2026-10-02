using FluentAssertions;
using NpiManagement.Application.Abstractions;
using NpiManagement.Application.Authorization;
using NpiManagement.Application.Exceptions;
using NpiManagement.Application.Npis.Dtos;
using NpiManagement.Application.Npis.Queries;
using NpiManagement.Application.Tests.Fakes;
using NpiManagement.Domain;
using NpiManagement.Domain.Common;
using Xunit;

namespace NpiManagement.Application.Tests.Npis;

public class GetNpiAssigneesQueryHandlerTests
{
    private static Npi CreateDraft(FakeNpiRepository repo, Guid creatorId)
    {
        var npi = Npi.CreateDraft(Guid.NewGuid(), new Actor(creatorId, "UsuarioComercial", "Luis Vega"), TestClock.Default(), new NpiCaptureDto { Cliente = "ACME" }.ToDomain(), Priority.Media);
        repo.Add(npi);
        return npi;
    }

    [Fact]
    public async Task A_non_admin_with_access_gets_names_instead_of_ids()
    {
        var repo = new FakeNpiRepository();
        var creatorId = Guid.NewGuid();
        var npi = CreateDraft(repo, creatorId);
        var directory = new FakeUserDirectory();
        directory.Users.Add(new UserSummaryDto(npi.AssignedCommercialUserId, "Luis Vega", "lvega@bioflex.mx", "UsuarioComercial", AreaType.Comercial, UserStatus.Active));
        var handler = new GetNpiAssigneesQueryHandler(repo, directory);

        var result = await handler.HandleAsync(new GetNpiAssigneesQuery(npi.Id, new CurrentUser(creatorId, "UsuarioComercial", AreaType.Comercial)), CancellationToken.None);

        result.Commercial.Should().Be(new NpiAssigneeDto(npi.AssignedCommercialUserId, "Luis Vega"));
        result.Engineering.Should().BeNull();
        result.Design.Should().BeNull();
    }

    [Fact]
    public async Task An_account_that_no_longer_exists_shows_a_placeholder_not_the_id()
    {
        var repo = new FakeNpiRepository();
        var creatorId = Guid.NewGuid();
        var npi = CreateDraft(repo, creatorId);
        var handler = new GetNpiAssigneesQueryHandler(repo, new FakeUserDirectory());

        var result = await handler.HandleAsync(new GetNpiAssigneesQuery(npi.Id, new CurrentUser(creatorId, "UsuarioComercial", AreaType.Comercial)), CancellationToken.None);

        result.Commercial.FullName.Should().Be(GetNpiAssigneesQueryHandler.UnknownUserName);
    }

    [Fact]
    public async Task Throws_NotFound_for_a_user_who_cannot_see_the_npi()
    {
        var repo = new FakeNpiRepository();
        var npi = CreateDraft(repo, Guid.NewGuid());
        var handler = new GetNpiAssigneesQueryHandler(repo, new FakeUserDirectory());
        var stranger = new CurrentUser(Guid.NewGuid(), "UsuarioIngenieria", AreaType.Ingenieria);

        var act = async () => await handler.HandleAsync(new GetNpiAssigneesQuery(npi.Id, stranger), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }
}
