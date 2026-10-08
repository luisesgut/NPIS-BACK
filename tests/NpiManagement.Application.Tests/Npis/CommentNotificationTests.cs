using FluentAssertions;
using NpiManagement.Application.Abstractions;
using NpiManagement.Application.Authorization;
using NpiManagement.Application.Npis.Commands;
using NpiManagement.Application.Npis.Dtos;
using NpiManagement.Application.Tests.Fakes;
using NpiManagement.Domain;
using NpiManagement.Domain.Common;
using NpiManagement.Domain.ValueObjects;
using Xunit;

namespace NpiManagement.Application.Tests.Npis;

/// <summary>Los comentarios deben llegar a quienes participan en el NPI (antes no avisaban a nadie).</summary>
public sealed class CommentNotificationTests
{
    [Fact]
    public async Task A_design_comment_reaches_commercial_and_engineering_but_not_its_author()
    {
        var clock = TestClock.Default();
        var calendar = new MexicoBusinessCalendar();
        var commercial = new Actor(Guid.NewGuid(), "UsuarioComercial", "Vendedor");
        var engineerId = Guid.NewGuid();
        var designerId = Guid.NewGuid();
        var npi = Npi.CreateDraft(Guid.NewGuid(), commercial, clock, new NpiCaptureDto().ToDomain(), Priority.Media, engineerId);
        npi.Submit(commercial, clock, calendar, NpiNumber.FromSequenceValue(7));
        npi.AssignDesign(designerId, new Actor(Guid.NewGuid(), "AdminDiseno", "Admin"), clock);
        var repo = new FakeNpiRepository();
        repo.Add(npi);
        var notifications = new FakeNotificationService();
        var handler = new AddCommentCommandHandler(repo, new FakeUnitOfWork(), clock, notifications);

        await handler.HandleAsync(new AddCommentCommand(npi.Id, new CurrentUser(designerId, "UsuarioDiseno", AreaType.Diseno), "Diseñadora", "¿El logo va en negro o en verde?"), CancellationToken.None);

        notifications.Sent.Select(n => n.RecipientUserId).Should().BeEquivalentTo(new[] { commercial.UserId, engineerId });
        notifications.Sent.Should().OnlyContain(n =>
            n.Message == "Diseñadora comentó en el NPI NPI-0007: ¿El logo va en negro o en verde?" && n.Channel == NotificationChannel.BellAndEmail);
    }
}
