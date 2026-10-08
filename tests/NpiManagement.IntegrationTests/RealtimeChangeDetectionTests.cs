using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NpiManagement.Application.Abstractions;
using NpiManagement.Domain;
using NpiManagement.Domain.Common;
using NpiManagement.Domain.ValueObjects;
using NpiManagement.Infrastructure.Notifications;
using NpiManagement.Infrastructure.Persistence;
using NpiManagement.Infrastructure.Persistence.Entities;
using Xunit;

namespace NpiManagement.IntegrationTests;

/// <summary>
/// Qué avisa EfUnitOfWork en tiempo real a partir del ChangeTracker. No toca la base: sólo se
/// adjuntan entidades al contexto (el proveedor de SQL Server no abre conexión para eso).
/// </summary>
public class RealtimeChangeDetectionTests
{
    private static readonly TestClock Clock = new(new DateTimeOffset(2026, 1, 5, 16, 0, 0, TimeSpan.Zero));
    private static readonly Actor Commercial = new(Guid.NewGuid(), "UsuarioComercial", "Vendedor");
    private static readonly Actor AdminDesign = new(Guid.NewGuid(), "AdminDiseno", "Admin Diseño");

    private static NpiManagementDbContext NewContext() =>
        new(new DbContextOptionsBuilder<NpiManagementDbContext>()
            .UseSqlServer("Server=(sin-servidor);Database=NoSeUsa;Trusted_Connection=True")
            .Options);

    private static EfUnitOfWork UnitOfWork(NpiManagementDbContext db) =>
        new(db, new PendingEmailNotifications(), null!, new NullRealtimeEvents(), NullLogger<EfUnitOfWork>.Instance);

    private static Npi SubmittedNpi()
    {
        var npi = TestDataFactory.NewDraft(Commercial, Clock);
        npi.Submit(Commercial, Clock, TestDataFactory.Calendar, NpiNumber.FromSequenceValue(1));
        return npi;
    }

    [Fact]
    public void A_reassignment_notifies_the_new_and_the_previous_designer()
    {
        var before = Guid.NewGuid();
        var after = Guid.NewGuid();
        var npi = SubmittedNpi();
        npi.AssignDesign(before, AdminDesign, Clock);
        using var db = NewContext();
        db.Attach(npi);

        npi.AssignDesign(after, AdminDesign, Clock);
        var changes = UnitOfWork(db).CollectChanges();

        changes.Npis.Should().ContainKey(npi.Id);
        changes.Npis[npi.Id].Should().Contain([Commercial.UserId, before, after]);
    }

    [Fact]
    public void Notifications_go_to_their_recipient_and_chat_reads_are_ignored()
    {
        var recipient = Guid.NewGuid();
        using var db = NewContext();
        db.Notifications.Add(new Notification { Id = Guid.NewGuid(), RecipientUserId = recipient, Message = "x", CreatedAt = Clock.UtcNow });
        db.NpiChatReads.Add(new NpiChatRead { NpiId = Guid.NewGuid(), UserId = Guid.NewGuid(), LastReadAt = Clock.UtcNow });

        var changes = UnitOfWork(db).CollectChanges();

        changes.NotificationRecipients.Should().Equal(recipient);
        changes.Npis.Should().BeEmpty("marcar el chat como leído no cambia el NPI");
    }

    [Fact]
    public void Nothing_changed_nothing_to_announce()
    {
        using var db = NewContext();
        db.Attach(SubmittedNpi());

        var changes = UnitOfWork(db).CollectChanges();

        changes.Npis.Should().BeEmpty();
        changes.NotificationRecipients.Should().BeEmpty();
    }
}
