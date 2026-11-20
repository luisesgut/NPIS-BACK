using FluentAssertions;
using NpiManagement.Application.Abstractions;
using NpiManagement.Application.Authorization;
using NpiManagement.Application.Npis.Dtos;
using NpiManagement.Application.Npis.Queries;
using NpiManagement.Application.Tests.Fakes;
using NpiManagement.Domain;
using NpiManagement.Domain.Common;
using Xunit;

namespace NpiManagement.Application.Tests.Npis;

/// <summary>PT en SAP por el código del PrintCard (2026-11-12).</summary>
public class GetSapProductsQueryHandlerTests
{
    private sealed class FakeSap(bool enabled) : ISapProductsReader
    {
        public bool IsEnabled => enabled;
        public int Calls { get; private set; }
        public Task<IReadOnlyList<SapProductDto>> FindByPrintCardAsync(string printCardCode, bool startsWith, CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult<IReadOnlyList<SapProductDto>>([]);
        }
    }

    [Theory]
    [InlineData("E-11854-A_R-2", "E-11854-A")]
    [InlineData("e-5098-a_r2", "e-5098-a")]
    [InlineData("PC-0451", null)]
    public void The_revision_is_dropped_for_the_approximate_search(string code, string? expected) =>
        GetSapProductsQueryHandler.WithoutRevision(code).Should().Be(expected);

    [Fact]
    public async Task Without_a_printcard_or_turned_off_it_does_not_ask_sap()
    {
        var repo = new FakeNpiRepository();
        var seller = Guid.NewGuid();
        var npi = Npi.CreateDraft(Guid.NewGuid(), new Actor(seller, "UsuarioComercial", "Vendedor"), TestClock.Default(), new NpiCaptureDto { Cliente = "DESTINY" }.ToDomain(), Priority.Media);
        repo.Add(npi);
        var user = new CurrentUser(seller, "UsuarioComercial", AreaType.Comercial);

        var on = new FakeSap(true);
        (await new GetSapProductsQueryHandler(repo, on).HandleAsync(new GetSapProductsQuery(npi.Id, user), CancellationToken.None))
            .Should().Be(new SapProductsLookupDto(true, null, []));
        on.Calls.Should().Be(0);

        var off = new FakeSap(false);
        (await new GetSapProductsQueryHandler(repo, off).HandleAsync(new GetSapProductsQuery(npi.Id, user), CancellationToken.None)).Enabled.Should().BeFalse();
    }
}
