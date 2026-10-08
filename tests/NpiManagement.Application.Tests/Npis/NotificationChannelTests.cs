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

/// <summary>
/// Fija el corte entre lo que sale por correo y lo que se queda en la campana.
///
/// Existe porque ese corte es una decisión de negocio que el código expresa con un parámetro
/// fácil de agregar por inercia al escribir un comando nuevo. Si alguien marca BellAndEmail en
/// todos los eventos "por consistencia", la gente empieza a recibir correo por cada movimiento de
/// cada NPI, filtra al remitente a una carpeta, y el sistema de avisos deja de servir por
/// completo — sin que nada falle ni se rompa ninguna prueba. Estas pruebas lo hacen visible.
/// </summary>
public class NotificationChannelTests
{
    private static (FakeNpiRepository Repo, Npi Npi, IClock Clock) NewActiveNpi()
    {
        var repo = new FakeNpiRepository();
        var clock = TestClock.Default();
        var creatorId = Guid.NewGuid();
        var creator = new Actor(creatorId, "UsuarioComercial", "Creador");

        var npi = Npi.CreateDraft(Guid.NewGuid(), creator, clock, new NpiCaptureDto().ToDomain(), Priority.Media);
        npi.Submit(creator, clock, new MexicoBusinessCalendar(), NpiNumber.FromSequenceValue(1));
        repo.Add(npi);

        return (repo, npi, clock);
    }

    [Fact]
    public async Task Assigning_someone_to_an_npi_also_sends_email()
    {
        var (repo, npi, clock) = NewActiveNpi();
        var notifications = new FakeNotificationService();
        var admin = new CurrentUser(Guid.NewGuid(), "AdminIngenieria", AreaType.Ingenieria);
        var engineerId = Guid.NewGuid();
        var users = new FakeUserDirectory().With(engineerId, AreaType.Ingenieria, "UsuarioIngenieria");
        var handler = new AssignEngineeringCommandHandler(repo, new FakeUnitOfWork(), clock, new MexicoBusinessCalendar(), notifications, users);

        await handler.HandleAsync(new AssignEngineeringCommand(npi.Id, admin, "Admin", engineerId), CancellationToken.None);

        // Una asignación exige que esa persona haga algo — no puede depender de que abra la app.
        var sent = notifications.Sent.Should().ContainSingle().Subject;
        sent.RecipientUserId.Should().Be(engineerId);
        sent.Channel.Should().Be(NotificationChannel.BellAndEmail);
    }

    [Fact]
    public async Task Progress_updates_stay_in_the_bell_and_do_not_send_email()
    {
        var (repo, npi, clock) = NewActiveNpi();
        var engineerId = Guid.NewGuid();
        var designerId = Guid.NewGuid();
        var adminActor = new Actor(Guid.NewGuid(), "AdminTotal", "Admin");
        npi.AssignEngineering(engineerId, adminActor, clock);
        npi.AssignDesign(designerId, adminActor, clock);

        var notifications = new FakeNotificationService();
        var handler = new ApproveEngineeringFeasibilityCommandHandler(repo, new FakeUnitOfWork(), clock, new MexicoBusinessCalendar(), notifications);
        var engineer = new CurrentUser(engineerId, "UsuarioIngenieria", AreaType.Ingenieria);

        await handler.HandleAsync(new ApproveEngineeringFeasibilityCommand(npi.Id, engineer, "Ingeniero", null), CancellationToken.None);

        // Que la otra área haya avanzado es informativo: no le pide nada a quien lo recibe.
        var sent = notifications.Sent.Should().ContainSingle().Subject;
        sent.RecipientUserId.Should().Be(designerId);
        sent.Channel.Should().Be(NotificationChannel.Bell);
    }

    [Fact]
    public void The_default_channel_is_the_quiet_one()
    {
        var notifications = new FakeNotificationService();

        // Un comando nuevo que olvide especificar el canal manda a la campana, nunca correo:
        // el valor por defecto tiene que ser el que no puede causar daño.
        notifications.Notify(Guid.NewGuid(), "Cualquier aviso", null);

        notifications.Sent.Should().ContainSingle().Which.Channel.Should().Be(NotificationChannel.Bell);
    }
}
