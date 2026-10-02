using FluentAssertions;
using NpiManagement.Application.Authorization;
using NpiManagement.Application.Exceptions;
using NpiManagement.Application.Npis.Dtos;
using NpiManagement.Application.Npis.Queries;
using NpiManagement.Application.Tests.Fakes;
using NpiManagement.Domain;
using NpiManagement.Domain.Common;
using Xunit;

namespace NpiManagement.Application.Tests.Npis;

public class GetNpiByIdQueryHandlerTests
{
    private static readonly IBusinessCalendar Calendar = new MexicoBusinessCalendar();

    [Fact]
    public async Task Returns_the_detail_dto_when_the_user_can_access_the_resource()
    {
        var clock = TestClock.Default();
        var repo = new FakeNpiRepository();
        var creatorId = Guid.NewGuid();
        var npi = Npi.CreateDraft(Guid.NewGuid(), new Actor(creatorId, "UsuarioComercial", "Uno"), clock, new NpiCaptureDto { Cliente = "ACME" }.ToDomain(), Priority.Media);
        repo.Add(npi);
        var handler = new GetNpiByIdQueryHandler(repo, clock, Calendar);

        var result = await handler.HandleAsync(new GetNpiByIdQuery(npi.Id, new CurrentUser(creatorId, "UsuarioComercial", AreaType.Comercial)), CancellationToken.None);

        result.Capture.Cliente.Should().Be("ACME");
        result.Id.Should().Be(npi.Id);
    }

    [Fact]
    public async Task Throws_NotFound_for_an_unrelated_user()
    {
        var clock = TestClock.Default();
        var repo = new FakeNpiRepository();
        var npi = Npi.CreateDraft(Guid.NewGuid(), new Actor(Guid.NewGuid(), "UsuarioComercial", "Uno"), clock, new NpiCaptureDto().ToDomain(), Priority.Media);
        repo.Add(npi);
        var handler = new GetNpiByIdQueryHandler(repo, clock, Calendar);
        var stranger = new CurrentUser(Guid.NewGuid(), "UsuarioIngenieria", AreaType.Ingenieria);

        var act = async () => await handler.HandleAsync(new GetNpiByIdQuery(npi.Id, stranger), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Throws_NotFound_when_the_npi_does_not_exist()
    {
        var handler = new GetNpiByIdQueryHandler(new FakeNpiRepository(), TestClock.Default(), Calendar);
        var admin = new CurrentUser(Guid.NewGuid(), "AdminTotal", AreaType.Total);

        var act = async () => await handler.HandleAsync(new GetNpiByIdQuery(Guid.NewGuid(), admin), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }
}
