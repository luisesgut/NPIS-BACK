using FluentAssertions;
using NpiManagement.Application.Authorization;
using NpiManagement.Application.Npis.Commands;
using NpiManagement.Application.Npis.Dtos;
using NpiManagement.Application.Tests.Fakes;
using NpiManagement.Domain;
using NpiManagement.Domain.Common;
using Xunit;

namespace NpiManagement.Application.Tests.Npis;

/// <summary>
/// Recorre el ciclo completo (Draft → Active → Completed) exclusivamente a través de los
/// command handlers de Application (repositorio y notificaciones en memoria, sin base de
/// datos), demostrando que la composición de autorización + dominio + notificaciones funciona
/// de punta a punta para el camino feliz descrito en docs/workflow.md.
/// </summary>
public class FullWorkflowLifecycleTests
{
    private static readonly Guid CommercialId = Guid.NewGuid();
    private static readonly Guid EngineerId = Guid.NewGuid();
    private static readonly Guid DesignerId = Guid.NewGuid();

    private static readonly CurrentUser Commercial = new(CommercialId, "UsuarioComercial", AreaType.Comercial);
    private static readonly CurrentUser EngineeringAdmin = new(Guid.NewGuid(), "AdminIngenieria", AreaType.Ingenieria);
    private static readonly CurrentUser DesignAdmin = new(Guid.NewGuid(), "AdminDiseno", AreaType.Diseno);
    private static readonly CurrentUser Engineer = new(EngineerId, "UsuarioIngenieria", AreaType.Ingenieria);
    private static readonly CurrentUser Designer = new(DesignerId, "UsuarioDiseno", AreaType.Diseno);

    [Fact]
    public async Task Submitting_without_artwork_is_allowed()
    {
        var repo = new FakeNpiRepository();
        var uow = new FakeUnitOfWork();
        var clock = TestClock.Default();
        var numbers = new FakeNpiNumberGenerator();
        var users = new FakeUserDirectory();
        var npiId = await new CreateNpiDraftCommandHandler(repo, uow, clock, users)
            .HandleAsync(new CreateNpiDraftCommand(CommercialId, "UsuarioComercial", "Comercial Uno", new NpiCaptureDto(), Priority.Media, null), CancellationToken.None);
        var submit = new SubmitNpiCommandHandler(repo, uow, clock, new MexicoBusinessCalendar(), numbers, new FakeNotificationService(), users);

        var sent = await submit.HandleAsync(new SubmitNpiCommand(npiId, Commercial, "Comercial Uno"), CancellationToken.None);
        sent.NpiNumber.Should().Be("NPI-0001");
    }

    [Fact]
    public async Task Happy_path_completes_the_cycle_and_fires_the_expected_notifications()
    {
        var repo = new FakeNpiRepository();
        var uow = new FakeUnitOfWork();
        var clock = TestClock.Default();
        var calendar = new MexicoBusinessCalendar();
        var numbers = new FakeNpiNumberGenerator();
        var notifications = new FakeNotificationService();
        var users = new FakeUserDirectory().With(EngineerId, AreaType.Ingenieria, "UsuarioIngenieria").With(DesignerId, AreaType.Diseno, "UsuarioDiseno");

        // --- 1. Crear borrador ---
        var createHandler = new CreateNpiDraftCommandHandler(repo, uow, clock, users);
        var npiId = await createHandler.HandleAsync(
            new CreateNpiDraftCommand(CommercialId, "UsuarioComercial", "Comercial Uno", new NpiCaptureDto { Cliente = "ACME" }, Priority.Media, null),
            CancellationToken.None);

        // --- 2. Enviar (asigna folio, arranca timers) ---
        var submitHandler = new SubmitNpiCommandHandler(repo, uow, clock, calendar, numbers, notifications, users);
        repo.AddArtwork(npiId);
        var afterSubmit = await submitHandler.HandleAsync(new SubmitNpiCommand(npiId, Commercial, "Comercial Uno"), CancellationToken.None);
        afterSubmit.NpiNumber.Should().Be("NPI-0001");
        afterSubmit.LifecycleStatus.Should().Be("Active");

        // --- 3. Asignaciones ---
        await new AssignEngineeringCommandHandler(repo, uow, clock, calendar, notifications, users)
            .HandleAsync(new AssignEngineeringCommand(npiId, EngineeringAdmin, "Admin Ing", EngineerId), CancellationToken.None);
        await new AssignDesignCommandHandler(repo, uow, clock, calendar, notifications, users)
            .HandleAsync(new AssignDesignCommand(npiId, DesignAdmin, "Admin Diseño", DesignerId, ArtClassification.C, false), CancellationToken.None);

        // --- 4. Factibilidades independientes (decisión 1) ---
        await new ApproveDesignFeasibilityCommandHandler(repo, uow, clock, calendar, notifications)
            .HandleAsync(new ApproveDesignFeasibilityCommand(npiId, Designer, "Diseñador Uno", "Ok diseño", null), CancellationToken.None);
        await new ApproveEngineeringFeasibilityCommandHandler(repo, uow, clock, calendar, notifications)
            .HandleAsync(new ApproveEngineeringFeasibilityCommand(npiId, Engineer, "Ingeniero Uno", "Ok ingeniería"), CancellationToken.None);

        // --- 5. Artes Validadas ---
        await new ValidateArtworkCommandHandler(repo, uow, clock, calendar)
            .HandleAsync(new ValidateArtworkCommand(npiId, Designer, "Diseñador Uno"), CancellationToken.None);

        // --- 5b. Cotización en SISPRO: Comercial la liga y el cliente acepta el precio ---
        var quotations = new FakeQuotationProvider().With("678");
        var linked = await new LinkSisproQuoteCommandHandler(repo, uow, clock, calendar, quotations, notifications)
            .HandleAsync(new LinkSisproQuoteCommand(npiId, Commercial, "Comercial Uno", "678"), CancellationToken.None);
        linked.SisproQuotes.Should().ContainSingle().Which.Status.Should().Be("AwaitingClient");
        await new AcceptSisproQuoteCommandHandler(repo, uow, clock, calendar, notifications)
            .HandleAsync(new AcceptSisproQuoteCommand(npiId, Commercial, "Comercial Uno", null, null), CancellationToken.None);
        notifications.Sent.Should().Contain(n => n.RecipientUserId == DesignerId && n.Message.Contains("aceptó el precio"));

        // --- 6. PrintCard: Diseño genera (queda aprobada por Diseño) → Ingeniería → cliente ---
        var createVersionHandler = new CreatePrintCardVersionCommandHandler(repo, uow, clock, calendar, notifications);
        await createVersionHandler.HandleAsync(new CreatePrintCardVersionCommand(npiId, Designer, "Diseñador Uno", "PC-0001", repo.AddFile(npiId, AttachmentContext.PrintCard), null), CancellationToken.None);

        var approveHandler = new ApprovePrintCardCommandHandler(repo, uow, clock, calendar, notifications);
        await approveHandler.HandleAsync(new ApprovePrintCardCommand(npiId, Engineer, "Ingeniero Uno", ApproverRole.Engineering, null, null), CancellationToken.None);
        await new SendPrintCardToClientCommandHandler(repo, uow, clock, calendar)
            .HandleAsync(new SendPrintCardToClientCommand(npiId, Commercial, "Comercial Uno"), CancellationToken.None);
        await approveHandler.HandleAsync(new ApprovePrintCardCommand(npiId, Commercial, "Comercial Uno", ApproverRole.Client, null, repo.AddFile(npiId, AttachmentContext.ClientApproval)), CancellationToken.None);

        // --- 7. Cargar PrintCard en sistema (no termina el ciclo — decisión 3) ---
        var afterLoad = await new LoadPrintCardIntoSystemCommandHandler(repo, uow, clock, calendar, notifications)
            .HandleAsync(new LoadPrintCardIntoSystemCommand(npiId, Designer, "Diseñador Uno"), CancellationToken.None);
        afterLoad.LifecycleStatus.Should().Be("Active");

        // --- 8. SAP + ET ---
        await new RegisterSapPtCommandHandler(repo, uow, clock, calendar, notifications)
            .HandleAsync(new RegisterSapPtCommand(npiId, Engineer, "Ingeniero Uno", "PT-001", "Nombre PT"), CancellationToken.None);
        await new RegisterSapOrderCommandHandler(repo, uow, clock, calendar, notifications)
            .HandleAsync(new RegisterSapOrderCommand(npiId, Commercial, "Comercial Uno", "4500012345"), CancellationToken.None);
        await new RegisterEtCommandHandler(repo, uow, clock, calendar)
            .HandleAsync(new RegisterEtCommand(npiId, Engineer, "Ingeniero Uno"), CancellationToken.None);
        var final = await new LoadEtIntoSisproCommandHandler(repo, uow, clock, calendar, notifications)
            .HandleAsync(new LoadEtIntoSisproCommand(npiId, Engineer, "Ingeniero Uno"), CancellationToken.None);

        // --- Verificaciones finales ---
        final.LifecycleStatus.Should().Be("Completed");
        final.GlobalTimer.IsClosed.Should().BeTrue();
        final.EngineeringTimer.IsClosed.Should().BeTrue();

        notifications.Sent.Should().Contain(n => n.RecipientUserId == EngineerId && n.Message.Contains("asignado"));
        notifications.Sent.Should().Contain(n => n.RecipientUserId == CommercialId && n.Message.Contains("completó el ciclo"));
        uow.SaveChangesCallCount.Should().BeGreaterThan(10, "cada acción de workflow debe confirmar su propia transacción");
    }

    [Fact]
    public async Task An_unassigned_engineer_cannot_approve_feasibility_for_a_npi_that_is_not_theirs()
    {
        var repo = new FakeNpiRepository();
        var uow = new FakeUnitOfWork();
        var clock = TestClock.Default();
        var calendar = new MexicoBusinessCalendar();
        var numbers = new FakeNpiNumberGenerator();
        var notifications = new FakeNotificationService();
        var users = new FakeUserDirectory().With(EngineerId, AreaType.Ingenieria, "UsuarioIngenieria").With(DesignerId, AreaType.Diseno, "UsuarioDiseno");

        var createHandler = new CreateNpiDraftCommandHandler(repo, uow, clock, users);
        var npiId = await createHandler.HandleAsync(
            new CreateNpiDraftCommand(CommercialId, "UsuarioComercial", "Comercial Uno", new NpiCaptureDto(), Priority.Media, null),
            CancellationToken.None);
        repo.AddArtwork(npiId);
        await new SubmitNpiCommandHandler(repo, uow, clock, calendar, numbers, notifications, users)
            .HandleAsync(new SubmitNpiCommand(npiId, Commercial, "Comercial Uno"), CancellationToken.None);
        await new AssignEngineeringCommandHandler(repo, uow, clock, calendar, notifications, users)
            .HandleAsync(new AssignEngineeringCommand(npiId, EngineeringAdmin, "Admin Ing", EngineerId), CancellationToken.None);

        var stranger = new CurrentUser(Guid.NewGuid(), "UsuarioIngenieria", AreaType.Ingenieria);
        var handler = new ApproveEngineeringFeasibilityCommandHandler(repo, uow, clock, calendar, notifications);
        var act = async () => await handler.HandleAsync(new ApproveEngineeringFeasibilityCommand(npiId, stranger, "Otro", null), CancellationToken.None);

        await act.Should().ThrowAsync<NpiManagement.Application.Exceptions.NotFoundException>();
    }
}
