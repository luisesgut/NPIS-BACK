using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NpiManagement.Domain;
using NpiManagement.Domain.Entities;
using NpiManagement.Domain.ValueObjects;
using NpiManagement.Infrastructure.Persistence.Repositories;
using Xunit;

namespace NpiManagement.IntegrationTests;

/// <summary>
/// Prueba de persistencia completa del agregado Npi (todas sus colecciones satélite) y de sus
/// relaciones/historial (auditoría, eventos de cronómetro, borrado en cascada) contra un SQL
/// Server real.
/// </summary>
[Collection(IntegrationTestCollection.Name)]
public class NpiAggregatePersistenceTests(SqlServerFixture fixture)
{
    [SkippableFact]
    public async Task Full_aggregate_round_trips_every_satellite_collection()
    {
        Skip.IfNot(fixture.IsAvailable, fixture.SkipReason);

        var clock = new TestClock(DateTimeOffset.UtcNow);
        Guid npiId;
        Guid commercialId, engineerId, designerId;

        // --- Construir un NPI rico a través de la API de dominio y persistirlo ---
        await using (var db = fixture.CreateDbContext())
        {
            var commercial = await TestDataFactory.AddUserAsync(db, "comercial", AreaType.Comercial);
            var engineer = await TestDataFactory.AddUserAsync(db, "ingeniero", AreaType.Ingenieria);
            var designer = await TestDataFactory.AddUserAsync(db, "disenador", AreaType.Diseno);
            commercialId = commercial.Id;
            engineerId = engineer.Id;
            designerId = designer.Id;

            var comActor = TestDataFactory.ActorFor(commercial, "UsuarioComercial");
            var engActor = TestDataFactory.ActorFor(engineer, "UsuarioIngenieria");
            var desActor = TestDataFactory.ActorFor(designer, "UsuarioDiseno");

            var npi = TestDataFactory.NewDraft(comActor, clock);
            npi.Submit(comActor, clock, TestDataFactory.Calendar, NpiNumber.FromSequenceValue(555001));
            npi.AssignEngineering(engineerId, comActor, clock);
            npi.AssignDesign(designerId, comActor, clock);

            // Genera un ChangeRequest antes de aprobar factibilidad.
            npi.RequestEngineeringChanges(engActor, clock, TestDataFactory.Calendar, "Falta el ancho exacto", ["ancho"]);
            npi.Submit(comActor, clock, TestDataFactory.Calendar); // reenvío tras cambios

            // Genera un InformationRequest resuelto.
            npi.RequestInformation(desActor, clock, TestDataFactory.Calendar, InformationRequestType.Commercial, "¿Contacto de compras?");
            npi.RespondCommercialInformation(comActor, clock, TestDataFactory.Calendar, "Es Juan Pérez, ext. 123");

            // Ambas factibilidades (independientes, decisión 1) + Artes Validadas.
            npi.ApproveEngineeringFeasibility(engActor, clock, TestDataFactory.Calendar, "Ingeniería factible");
            npi.ApproveDesignFeasibility(desActor, clock, "Diseño factible");
            npi.ValidateArtwork(desActor, clock);

            // Adjunto suelto (contexto Comment).
            npi.RegisterAttachment(comActor, clock, "nota.pdf", "application/pdf", 1024, "storage/nota.pdf", null, AttachmentContext.Comment);

            // PrintCard con las 3 aprobaciones.
            npi.CreatePrintCardVersion(desActor, clock, TestDataFactory.Calendar, "PC-INT-001", null, "Primera versión");
            npi.ApprovePrintCard(desActor, clock, TestDataFactory.Calendar, ApproverRole.Design, null, null);
            npi.ApprovePrintCard(engActor, clock, TestDataFactory.Calendar, ApproverRole.Engineering, null, null);
            npi.SendPrintCardToClient(comActor, clock);
            npi.ApprovePrintCard(comActor, clock, TestDataFactory.Calendar, ApproverRole.Client, "Cliente aprobó", Guid.NewGuid());

            npiId = npi.Id;

            var repository = new NpiRepository(db);
            repository.Add(npi);
            await db.SaveChangesAsync();
        }

        // --- Releer desde un DbContext completamente nuevo (sin cache de identidad) ---
        await using var freshDb = fixture.CreateDbContext();
        var freshRepository = new NpiRepository(freshDb);
        var reloaded = await freshRepository.GetByIdAsync(npiId, CancellationToken.None);

        reloaded.Should().NotBeNull();
        reloaded!.Capture.Cliente.Should().Be("Cliente Integración");
        reloaded.LifecycleStatus.Should().Be(LifecycleStatus.Active);
        reloaded.AssignedEngineeringUserId.Should().Be(engineerId);
        reloaded.AssignedDesignUserId.Should().Be(designerId);

        reloaded.Attachments.Should().ContainSingle(a => a.FileName == "nota.pdf" && a.Context == AttachmentContext.Comment);
        reloaded.ChangeRequests.Should().ContainSingle(cr => cr.FromArea == AreaType.Ingenieria && cr.ToArea == AreaType.Comercial);
        reloaded.ChangeRequests.Single().FieldKeys.Should().BeEquivalentTo(["ancho"]);
        reloaded.InformationRequests.Should().ContainSingle(r => r.Type == InformationRequestType.Commercial && r.Status == InformationRequestStatus.Resolved);
        reloaded.InformationRequests.Single().ResponseText.Should().Contain("Juan Pérez");

        reloaded.FeasibilityReviews.Should().HaveCount(2);
        reloaded.FeasibilityReviews.Should().Contain(f => f.Area == AreaType.Ingenieria && f.Note == "Ingeniería factible");
        reloaded.FeasibilityReviews.Should().Contain(f => f.Area == AreaType.Diseno && f.Note == "Diseño factible");

        reloaded.PrintCardVersions.Should().ContainSingle();
        var version = reloaded.PrintCardVersions.Single();
        version.Code.Should().Be("PC-INT-001");
        version.Approvals.Should().HaveCount(3);
        version.Approvals.Should().Contain(a => a.Role == ApproverRole.Client && a.EvidenceAttachmentId != null);
        version.IsFullyApproved.Should().BeTrue();

        reloaded.TimerEventLog.Should().NotBeEmpty();
        reloaded.AuditLog.Should().NotBeEmpty();
        reloaded.AuditLog.Should().Contain(e => e.Action == "NpiSubmitted");
        reloaded.AuditLog.Should().Contain(e => e.Action == "PrintCardFullyApproved");

        reloaded.GlobalTimer.HasStarted.Should().BeTrue();
        reloaded.EngineeringTimer.HasStarted.Should().BeTrue();
        reloaded.DesignTimer.HasStarted.Should().BeTrue();
    }

    [SkippableFact]
    public async Task Deleting_an_npi_cascades_to_every_satellite_table()
    {
        Skip.IfNot(fixture.IsAvailable, fixture.SkipReason);

        var clock = new TestClock(DateTimeOffset.UtcNow);
        Guid npiId;

        await using (var db = fixture.CreateDbContext())
        {
            var user = await TestDataFactory.AddUserAsync(db, "cascadeowner", AreaType.Comercial);
            var actor = TestDataFactory.ActorFor(user, "UsuarioComercial");
            var npi = TestDataFactory.NewDraft(actor, clock);
            db.NpiComments.Add(NpiComment.Create(npi.Id, actor, clock, "Mensaje del chat que debe borrarse en cascada"));
            npi.RegisterAttachment(actor, clock, "a.pdf", "application/pdf", 10, "k1", null, AttachmentContext.Comment);
            npiId = npi.Id;

            db.Npis.Add(npi);
            await db.SaveChangesAsync();
        }

        await using (var db = fixture.CreateDbContext())
        {
            var npi = await db.Npis.SingleAsync(n => n.Id == npiId);
            db.Npis.Remove(npi);
            await db.SaveChangesAsync();
        }

        await using var verifyDb = fixture.CreateDbContext();
        (await verifyDb.Database.SqlQueryRaw<int>(
            "SELECT COUNT(*) AS Value FROM NpiComments WHERE NpiId = {0}", npiId).SingleAsync())
            .Should().Be(0, "el borrado en cascada debe eliminar los comentarios del NPI");
        (await verifyDb.Database.SqlQueryRaw<int>(
            "SELECT COUNT(*) AS Value FROM Attachments WHERE NpiId = {0}", npiId).SingleAsync())
            .Should().Be(0, "el borrado en cascada debe eliminar los adjuntos del NPI");
    }
}
