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

/// <summary>Órdenes de venta de SAP por el PT del NPI (2026-11-12).</summary>
public class GetSapOrdersQueryHandlerTests
{
    private sealed class FakeSap(bool enabled, Func<string, IReadOnlyList<SapOrderDto>> find) : ISapOrdersReader
    {
        public bool IsEnabled => enabled;
        public string? AskedFor { get; private set; }
        public Task<IReadOnlyList<SapOrderDto>> FindByProductKeyAsync(string claveProducto, CancellationToken cancellationToken)
        {
            AskedFor = claveProducto;
            return Task.FromResult(find(claveProducto));
        }
    }

    private static readonly Guid Seller = Guid.NewGuid();
    private static CurrentUser SellerUser => new(Seller, "UsuarioComercial", AreaType.Comercial);

    private static Npi NpiWithPt(FakeNpiRepository repo, string? pt)
    {
        var npi = Npi.CreateDraft(Guid.NewGuid(), new Actor(Seller, "UsuarioComercial", "Vendedor"), TestClock.Default(), new NpiCaptureDto { Cliente = "DESTINY" }.ToDomain(), Priority.Media);
        // El PT lo registra Ingeniería al final del flujo; para esta consulta basta con tenerlo.
        typeof(Npi).GetProperty(nameof(Npi.CodigoPT))!.SetValue(npi, pt);
        repo.Add(npi);
        return npi;
    }

    private static SapOrderDto Order(string orden) => new(orden, "PT12547", "BELLPEPPER", 10, "Millares", "DESTINY PACKAGING", null, null);

    [Fact]
    public async Task Turned_off_it_says_so_and_does_not_ask_sap()
    {
        var repo = new FakeNpiRepository();
        var npi = NpiWithPt(repo, "PT12547");
        var sap = new FakeSap(false, _ => throw new InvalidOperationException("no debe llamarse"));

        var result = await new GetSapOrdersQueryHandler(repo, sap).HandleAsync(new GetSapOrdersQuery(npi.Id, SellerUser), CancellationToken.None);

        result.Enabled.Should().BeFalse();
        sap.AskedFor.Should().BeNull();
    }

    [Fact]
    public async Task It_looks_up_by_pt_and_the_lowest_order_number_comes_first()
    {
        var repo = new FakeNpiRepository();
        var npi = NpiWithPt(repo, " PT12547 ");
        var sap = new FakeSap(true, _ => [Order("37100"), Order("9999"), Order("37057")]);

        var result = await new GetSapOrdersQueryHandler(repo, sap).HandleAsync(new GetSapOrdersQuery(npi.Id, SellerUser), CancellationToken.None);

        sap.AskedFor.Should().Be("PT12547");
        result.Items.Select(o => o.Orden).Should().Equal("9999", "37057", "37100");
    }

    [Fact]
    public async Task If_sap_fails_it_never_breaks_the_npi()
    {
        var repo = new FakeNpiRepository();
        var npi = NpiWithPt(repo, "PT12547");
        var sap = new FakeSap(true, _ => throw new TimeoutException());

        var result = await new GetSapOrdersQueryHandler(repo, sap).HandleAsync(new GetSapOrdersQuery(npi.Id, SellerUser), CancellationToken.None);

        result.Items.Should().BeEmpty();
        result.Error.Should().Contain("SAP no respondió");
    }
}
