using FluentAssertions;
using NpiManagement.Application.Abstractions;
using NpiManagement.Application.Authorization;
using NpiManagement.Application.Exceptions;
using NpiManagement.Application.Npis.Commands;
using NpiManagement.Application.Npis.Dtos;
using NpiManagement.Application.Npis.Queries;
using NpiManagement.Application.Tests.Fakes;
using NpiManagement.Domain;
using NpiManagement.Domain.Common;
using NpiManagement.Domain.ValueObjects;
using Xunit;

namespace NpiManagement.Application.Tests.Npis;

public sealed class CaptureHistoryQueryTests
{
    private readonly TestClock _clock = TestClock.Default();
    private readonly MexicoBusinessCalendar _calendar = new();
    private readonly Actor _commercial = new(Guid.NewGuid(), "UsuarioComercial", "Vendedor");
    private readonly Guid _engineerId = Guid.NewGuid();
    private readonly FakeNpiRepository _repo = new();
    private readonly FakeCaptureHistoryStore _history = new();
    private readonly FakeUserDirectory _users = new();

    private CurrentUser CommercialUser => new(_commercial.UserId, "UsuarioComercial", AreaType.Comercial);

    [Fact]
    public async Task Shows_the_initial_capture_and_who_requested_and_who_made_each_change()
    {
        _users.Users.Add(new UserSummaryDto(_commercial.UserId, "Vendedor", "v@bioflex.mx", "UsuarioComercial", AreaType.Comercial, UserStatus.Active));
        var npi = Npi.CreateDraft(Guid.NewGuid(), _commercial, _clock, new NpiCaptureDto { Ancho = "100" }.ToDomain(), Priority.Media, _engineerId);
        npi.Submit(_commercial, _clock, _calendar, NpiNumber.FromSequenceValue(3));
        npi.RequestEngineeringChanges(new Actor(_engineerId, "UsuarioIngenieria", "Ingeniero"), _clock, _calendar, "El ancho es 120", ["ancho"]);
        _repo.Add(npi);

        await new UpdateNpiDraftCommandHandler(_repo, new FakeUnitOfWork(), _clock, _calendar, _history)
            .HandleAsync(new UpdateNpiDraftCommand(npi.Id, CommercialUser, NpiCaptureDto.FromDomain(npi.Capture) with { Ancho = "120" }, Priority.Alta, _repo.GetRowVersion(npi), "Vendedor"), CancellationToken.None);

        var history = await new GetCaptureHistoryQueryHandler(_repo, _history, _users)
            .HandleAsync(new GetCaptureHistoryQuery(npi.Id, CommercialUser), CancellationToken.None);

        history.InitialCapture!.Ancho.Should().Be("100");
        history.InitialPriority.Should().Be("Media");
        history.FirstSubmittedByName.Should().Be("Vendedor");
        var ancho = history.Changes.Single(c => c.Field == "ancho");
        ancho.Should().Match<CaptureChangeDto>(c =>
            c.Before == "100" && c.After == "120" && c.ChangedByName == "Vendedor"
            && c.WasRequested && c.RequestedByName == "Ingeniero" && c.RequestedFromArea == "Ingenieria" && c.RequestDescription == "El ancho es 120");
        history.Changes.Single(c => c.Field == "priority").After.Should().Be("Alta");
    }

    [Fact]
    public async Task An_unrelated_user_cannot_see_the_history()
    {
        var npi = Npi.CreateDraft(Guid.NewGuid(), _commercial, _clock, new NpiCaptureDto().ToDomain(), Priority.Media);
        _repo.Add(npi);

        var act = async () => await new GetCaptureHistoryQueryHandler(_repo, _history, _users)
            .HandleAsync(new GetCaptureHistoryQuery(npi.Id, new CurrentUser(Guid.NewGuid(), "UsuarioIngenieria", AreaType.Ingenieria)), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }
}
