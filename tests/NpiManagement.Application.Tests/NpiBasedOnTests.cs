using FluentAssertions;
using NpiManagement.Application.Npis.Queries;
using NpiManagement.Domain;
using NpiManagement.Domain.Common;
using NpiManagement.Domain.ValueObjects;
using Xunit;

namespace NpiManagement.Application.Tests;

/// <summary>"Partir de un NPI anterior" (2026-11-11).</summary>
public class NpiBasedOnTests
{
    [Fact]
    public void Only_the_fields_that_really_changed_are_listed()
    {
        var before = NpiCaptureData.Empty with { Cliente = "DESTINY", Ancho = "600", Alto = "300", Descripcion = "BOLSA A", DescripcionProducto = "BOLSA A" };
        var after = before with { Ancho = "650", Descripcion = "bolsa b ", DescripcionProducto = "BOLSA B", Cliente = " destiny" };

        GetNpiBasedOnQueryHandler.ChangedFields(before, after).Should().BeEquivalentTo(["ancho", "descripcion"]);
    }

    [Fact]
    public void The_origin_is_set_once_and_only_on_a_draft()
    {
        var clock = TestClock.Default();
        var actor = new Actor(Guid.NewGuid(), "UsuarioComercial", "Vendedor");
        var npi = Npi.CreateDraft(Guid.NewGuid(), actor, clock, NpiCaptureData.Empty, Priority.Media);
        var source = Guid.NewGuid();

        npi.MarkBasedOn(source);
        npi.MarkBasedOn(Guid.NewGuid());
        npi.MarkBasedOn(npi.Id);

        npi.BasedOnNpiId.Should().Be(source);
    }
}
