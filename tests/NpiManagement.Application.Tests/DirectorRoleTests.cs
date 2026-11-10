using FluentAssertions;
using NpiManagement.Application.Authorization;
using NpiManagement.Domain;
using Xunit;

namespace NpiManagement.Application.Tests;

/// <summary>Rol Director (2026-11-09): ve todo, no opera nada.</summary>
public class DirectorRoleTests
{
    private static readonly CurrentUser Director = new(Guid.NewGuid(), ApplicationRoles.Director, AreaType.Total);
    private static readonly NpiOwnership SomeoneElses = new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

    [Fact]
    public void Es_un_rol_valido_de_todas_las_areas_y_no_es_admin()
    {
        ApplicationRoles.Exists(ApplicationRoles.Director).Should().BeTrue();
        ApplicationRoles.AreaFor(ApplicationRoles.Director).Should().Be(AreaType.Total);
        ApplicationRoles.IsAdmin(ApplicationRoles.Director).Should().BeFalse();
    }

    [Fact]
    public void Ve_todos_los_NPIs_aunque_no_sean_suyos()
    {
        NpiAuthorization.SeesAllNpis(Director).Should().BeTrue();
        NpiAuthorization.CanAccess(Director, SomeoneElses).Should().BeTrue();
        // La lista y los tableros se filtran con este predicado: debe dejar pasar todo.
        NpiAuthorization.BuildAccessPredicate(Director).Compile()(null!).Should().BeTrue();
    }

    [Fact]
    public void No_puede_actuar_como_ninguna_area()
    {
        NpiAuthorization.CanActAsEngineering(Director, (Guid?)null).Should().BeFalse();
        NpiAuthorization.CanActAsDesign(Director, (Guid?)null).Should().BeFalse();
        NpiAuthorization.CanActAsCommercial(Director, Guid.NewGuid(), Guid.NewGuid()).Should().BeFalse();
        NpiAuthorization.CanAssign(Director, AreaType.Diseno).Should().BeFalse();
    }
}
