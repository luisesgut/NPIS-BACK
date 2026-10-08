using FluentAssertions;
using NpiManagement.Application.Authorization;
using NpiManagement.Application.Exceptions;
using NpiManagement.Application.Npis.Commands;
using NpiManagement.Application.Npis.Dtos;
using NpiManagement.Application.Tests.Fakes;
using NpiManagement.Domain;
using NpiManagement.Domain.Common;
using Xunit;

namespace NpiManagement.Application.Tests.Npis;

public class AddCommentCommandHandlerTests
{
    [Fact]
    public async Task Adds_a_comment_visible_to_anyone_with_read_access()
    {
        var repo = new FakeNpiRepository();
        var creatorId = Guid.NewGuid();
        var npi = Npi.CreateDraft(Guid.NewGuid(), new Actor(creatorId, "UsuarioComercial", "Creador"), TestClock.Default(), new NpiCaptureDto().ToDomain(), Priority.Media);
        repo.Add(npi);
        var handler = new AddCommentCommandHandler(repo, new FakeUnitOfWork(), TestClock.Default(), new FakeNotificationService());
        var actor = new CurrentUser(creatorId, "UsuarioComercial", AreaType.Comercial);

        var comment = await handler.HandleAsync(new AddCommentCommand(npi.Id, actor, "Creador", "Falta información de compras"), CancellationToken.None);

        comment.Text.Should().Be("Falta información de compras");
        (await repo.GetByIdAsync(npi.Id, CancellationToken.None))!.Comments.Should().ContainSingle();
    }

    [Fact]
    public async Task Commenting_is_still_allowed_after_the_npi_becomes_terminal()
    {
        var repo = new FakeNpiRepository();
        var clock = TestClock.Default();
        var creatorId = Guid.NewGuid();
        var actorDomain = new Actor(creatorId, "UsuarioIngenieria", "Ingeniero");
        var npi = Npi.CreateDraft(Guid.NewGuid(), new Actor(creatorId, "UsuarioComercial", "Creador"), clock, new NpiCaptureDto().ToDomain(), Priority.Media);
        npi.Submit(new Actor(creatorId, "UsuarioComercial", "Creador"), clock, new MexicoBusinessCalendar(), NpiManagement.Domain.ValueObjects.NpiNumber.FromSequenceValue(1));
        npi.RejectNoFeasibility(actorDomain, clock, new MexicoBusinessCalendar(), null);
        repo.Add(npi);

        var handler = new AddCommentCommandHandler(repo, new FakeUnitOfWork(), clock, new FakeNotificationService());
        var actor = new CurrentUser(creatorId, "UsuarioComercial", AreaType.Comercial);

        var act = async () => await handler.HandleAsync(new AddCommentCommand(npi.Id, actor, "Creador", "Nota histórica tras el cierre"), CancellationToken.None);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task An_unrelated_user_cannot_comment()
    {
        var repo = new FakeNpiRepository();
        var creatorId = Guid.NewGuid();
        var npi = Npi.CreateDraft(Guid.NewGuid(), new Actor(creatorId, "UsuarioComercial", "Creador"), TestClock.Default(), new NpiCaptureDto().ToDomain(), Priority.Media);
        repo.Add(npi);
        var handler = new AddCommentCommandHandler(repo, new FakeUnitOfWork(), TestClock.Default(), new FakeNotificationService());
        var stranger = new CurrentUser(Guid.NewGuid(), "UsuarioComercial", AreaType.Comercial);

        var act = async () => await handler.HandleAsync(new AddCommentCommand(npi.Id, stranger, "Extraño", "Intento no autorizado"), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }
}
