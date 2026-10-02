using FluentAssertions;
using NpiManagement.Application.Authorization;
using NpiManagement.Domain;
using Xunit;

namespace NpiManagement.Application.Tests;

public class NpiAuthorizationTests
{
    private static readonly Guid Creator = Guid.NewGuid();
    private static readonly Guid AssignedCommercial = Guid.NewGuid();
    private static readonly Guid AssignedEngineer = Guid.NewGuid();
    private static readonly Guid AssignedDesigner = Guid.NewGuid();
    private static readonly Guid Stranger = Guid.NewGuid();

    private static readonly NpiOwnership Ownership = new(Creator, AssignedCommercial, AssignedEngineer, AssignedDesigner);

    [Theory]
    [InlineData("AdminTotal")]
    [InlineData("AdminComercial")]
    [InlineData("AdminIngenieria")]
    [InlineData("AdminDiseno")]
    public void Admins_can_access_any_npi_regardless_of_assignment(string role)
    {
        var user = new CurrentUser(Stranger, role, AreaType.Total);

        NpiAuthorization.CanAccess(user, Ownership).Should().BeTrue();
    }

    [Fact]
    public void UsuarioComercial_can_access_when_creator()
    {
        var user = new CurrentUser(Creator, "UsuarioComercial", AreaType.Comercial);
        NpiAuthorization.CanAccess(user, Ownership).Should().BeTrue();
    }

    [Fact]
    public void UsuarioComercial_can_access_when_assigned_as_commercial_owner()
    {
        var user = new CurrentUser(AssignedCommercial, "UsuarioComercial", AreaType.Comercial);
        NpiAuthorization.CanAccess(user, Ownership).Should().BeTrue();
    }

    [Fact]
    public void UsuarioComercial_cannot_access_an_unrelated_npi()
    {
        var user = new CurrentUser(Stranger, "UsuarioComercial", AreaType.Comercial);
        NpiAuthorization.CanAccess(user, Ownership).Should().BeFalse();
    }

    [Fact]
    public void UsuarioIngenieria_can_access_only_when_assigned()
    {
        var assigned = new CurrentUser(AssignedEngineer, "UsuarioIngenieria", AreaType.Ingenieria);
        var unassigned = new CurrentUser(Stranger, "UsuarioIngenieria", AreaType.Ingenieria);

        NpiAuthorization.CanAccess(assigned, Ownership).Should().BeTrue();
        NpiAuthorization.CanAccess(unassigned, Ownership).Should().BeFalse();
    }

    [Fact]
    public void UsuarioIngenieria_cannot_access_via_design_or_commercial_assignment()
    {
        var user = new CurrentUser(AssignedDesigner, "UsuarioIngenieria", AreaType.Ingenieria);
        NpiAuthorization.CanAccess(user, Ownership).Should().BeFalse("estar asignado en Diseño no da acceso como Ingeniería");
    }

    [Fact]
    public void UsuarioDiseno_can_access_only_when_assigned()
    {
        var assigned = new CurrentUser(AssignedDesigner, "UsuarioDiseno", AreaType.Diseno);
        var unassigned = new CurrentUser(Stranger, "UsuarioDiseno", AreaType.Diseno);

        NpiAuthorization.CanAccess(assigned, Ownership).Should().BeTrue();
        NpiAuthorization.CanAccess(unassigned, Ownership).Should().BeFalse();
    }

    [Fact]
    public void Unassigned_engineering_or_design_denies_regular_users()
    {
        var ownershipNoAssignments = new NpiOwnership(Creator, AssignedCommercial, null, null);
        var ingUser = new CurrentUser(Guid.NewGuid(), "UsuarioIngenieria", AreaType.Ingenieria);
        var desUser = new CurrentUser(Guid.NewGuid(), "UsuarioDiseno", AreaType.Diseno);

        NpiAuthorization.CanAccess(ingUser, ownershipNoAssignments).Should().BeFalse();
        NpiAuthorization.CanAccess(desUser, ownershipNoAssignments).Should().BeFalse();
    }

    [Fact]
    public void Unknown_role_is_denied_by_default()
    {
        var user = new CurrentUser(Creator, "SomeFutureRole", AreaType.Comercial);
        NpiAuthorization.CanAccess(user, Ownership).Should().BeFalse();
    }

    [Theory]
    [InlineData("AdminTotal", AreaType.Comercial, true)]
    [InlineData("AdminTotal", AreaType.Ingenieria, true)]
    [InlineData("AdminComercial", AreaType.Comercial, true)]
    [InlineData("AdminComercial", AreaType.Ingenieria, false)]
    [InlineData("AdminIngenieria", AreaType.Ingenieria, true)]
    [InlineData("AdminDiseno", AreaType.Diseno, true)]
    [InlineData("UsuarioComercial", AreaType.Comercial, false)]
    public void CanAssign_only_allows_the_matching_area_admin_or_AdminTotal(string role, AreaType area, bool expected)
    {
        var user = new CurrentUser(Guid.NewGuid(), role, AreaType.Total);
        NpiAuthorization.CanAssign(user, area).Should().Be(expected);
    }

    [Theory]
    [InlineData("AdminTotal")]
    [InlineData("AdminComercial")]
    [InlineData("AdminIngenieria")]
    [InlineData("AdminDiseno")]
    [InlineData("UsuarioComercial")]
    [InlineData("UsuarioIngenieria")]
    [InlineData("UsuarioDiseno")]
    [InlineData("Stranger")]
    public void BuildAccessPredicate_agrees_with_CanAccess_for_every_role(string role)
    {
        // La regla de listado (predicado traducible a SQL) y la de detalle (CanAccess) deben
        // decidir exactamente lo mismo para el mismo usuario y el mismo NPI — si divergen, un
        // usuario podría ver en el listado NPIs que luego no puede abrir, o viceversa.
        foreach (var actingAs in new[] { Creator, AssignedCommercial, AssignedEngineer, AssignedDesigner, Stranger })
        {
            var user = new CurrentUser(actingAs, role, AreaType.Comercial);
            var viaCanAccess = NpiAuthorization.CanAccess(user, Ownership);

            var predicate = NpiAuthorization.BuildAccessPredicate(user).Compile();
            var npi = BuildNpiMatchingOwnership();
            var viaPredicate = predicate(npi);

            viaPredicate.Should().Be(viaCanAccess, $"rol={role}, actor={actingAs}");
        }
    }

    private static Npi BuildNpiMatchingOwnership()
    {
        var clock = TestClock.Default();
        var creatorActor = new NpiManagement.Domain.Common.Actor(Creator, "UsuarioComercial", "Creador");
        var npi = Npi.CreateDraft(Guid.NewGuid(), creatorActor, clock, NpiManagement.Domain.ValueObjects.NpiCaptureData.Empty, Priority.Media);
        npi.ReassignCommercial(AssignedCommercial, creatorActor, clock);
        npi.AssignEngineering(AssignedEngineer, new NpiManagement.Domain.Common.Actor(Creator, "AdminIngenieria", "Admin"), clock);
        npi.AssignDesign(AssignedDesigner, new NpiManagement.Domain.Common.Actor(Creator, "AdminDiseno", "Admin"), clock);
        return npi;
    }
}
