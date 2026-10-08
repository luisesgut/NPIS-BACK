using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NpiManagement.Domain;
using NpiManagement.Domain.Common;
using NpiManagement.Domain.ValueObjects;
using Xunit;

namespace NpiManagement.IntegrationTests;

/// <summary>
/// Prueba de configuraciones/restricciones importantes, rowversion e índice único de NpiNumber
/// contra un SQL Server real (Testcontainers). Ver SqlServerFixture para cómo ejecutarlas.
/// </summary>
[Collection(IntegrationTestCollection.Name)]
public class ConfigurationAndConstraintsTests(SqlServerFixture fixture)
{
    [SkippableFact]
    public async Task Multiple_drafts_without_NpiNumber_are_allowed_by_the_filtered_unique_index()
    {
        Skip.IfNot(fixture.IsAvailable, fixture.SkipReason);
        await using var db = fixture.CreateDbContext();
        var clock = new TestClock(DateTimeOffset.UtcNow);
        var user = await TestDataFactory.AddUserAsync(db, "draftowner", AreaType.Comercial);
        var actor = TestDataFactory.ActorFor(user, "UsuarioComercial");

        var draft1 = TestDataFactory.NewDraft(actor, clock);
        var draft2 = TestDataFactory.NewDraft(actor, clock);
        db.Npis.AddRange(draft1, draft2);

        var act = async () => await db.SaveChangesAsync();

        await act.Should().NotThrowAsync("SQL Server sólo restringe un NULL por índice único normal; el índice filtrado excluye los NULL");
    }

    [SkippableFact]
    public async Task Two_active_npis_cannot_share_the_same_NpiNumber()
    {
        Skip.IfNot(fixture.IsAvailable, fixture.SkipReason);
        await using var db = fixture.CreateDbContext();
        var clock = new TestClock(DateTimeOffset.UtcNow);
        var user = await TestDataFactory.AddUserAsync(db, "dupnumber", AreaType.Comercial);
        var actor = TestDataFactory.ActorFor(user, "UsuarioComercial");

        var n1 = TestDataFactory.NewDraft(actor, clock);
        n1.Submit(actor, clock, TestDataFactory.Calendar, NpiNumber.FromSequenceValue(777001));
        db.Npis.Add(n1);
        await db.SaveChangesAsync();

        await using var db2 = fixture.CreateDbContext();
        var n2 = TestDataFactory.NewDraft(actor, clock);
        n2.Submit(actor, clock, TestDataFactory.Calendar, NpiNumber.FromSequenceValue(777001)); // mismo número a propósito
        db2.Npis.Add(n2);

        var act = async () => await db2.SaveChangesAsync();

        await act.Should().ThrowAsync<DbUpdateException>("IX_Npis_NpiNumber es único");
    }

    [SkippableFact]
    public async Task RowVersion_changes_after_an_update_and_a_stale_write_is_rejected()
    {
        Skip.IfNot(fixture.IsAvailable, fixture.SkipReason);
        var clock = new TestClock(DateTimeOffset.UtcNow);
        Guid npiId;

        await using (var db = fixture.CreateDbContext())
        {
            var user = await TestDataFactory.AddUserAsync(db, "rowversion", AreaType.Comercial);
            var actor = TestDataFactory.ActorFor(user, "UsuarioComercial");
            var npi = TestDataFactory.NewDraft(actor, clock);
            npiId = npi.Id;
            db.Npis.Add(npi);
            await db.SaveChangesAsync();
        }

        // Dos contextos cargan el mismo NPI ("dos usuarios" concurrentes).
        await using var dbA = fixture.CreateDbContext();
        await using var dbB = fixture.CreateDbContext();
        var npiA = await dbA.Npis.SingleAsync(n => n.Id == npiId);
        var npiB = await dbB.Npis.SingleAsync(n => n.Id == npiId);

        // Cambio sobre la fila del propio Npi (no una colección satélite), que es lo que verifica el RowVersion.
        npiA.UpdateDraft(npiA.Capture, Priority.Alta);
        await dbA.SaveChangesAsync(); // gana la primera escritura, RowVersion avanza en la BD

        npiB.UpdateDraft(npiB.Capture, Priority.Baja); // RowVersion desactualizado
        var act = async () => await dbB.SaveChangesAsync();

        await act.Should().ThrowAsync<DbUpdateConcurrencyException>("dbB todavía tiene el RowVersion anterior a la escritura de dbA");
    }
}
