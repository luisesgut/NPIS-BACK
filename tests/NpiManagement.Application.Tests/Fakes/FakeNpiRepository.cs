using NpiManagement.Application.Abstractions;
using NpiManagement.Domain;

namespace NpiManagement.Application.Tests.Fakes;

/// <summary>Repositorio en memoria — permite probar los command/query handlers sin SQL Server real.</summary>
public sealed class FakeNpiRepository : INpiRepository
{
    private readonly Dictionary<Guid, Npi> _store = [];

    public void Add(Npi npi) => _store[npi.Id] = npi;

    public Task<Npi?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(_store.GetValueOrDefault(id));

    public Task<Npi?> GetByNpiNumberAsync(string npiNumber, CancellationToken cancellationToken) =>
        Task.FromResult(_store.Values.FirstOrDefault(n => n.NpiNumber?.Value == npiNumber));

    // Esta fake no simula conflictos de concurrencia reales (eso se prueba contra SQL Server en
    // NpiManagement.IntegrationTests) — sólo necesita devolver un token estable y determinista
    // para que los handlers puedan construir NpiDetailDto.RowVersion en las pruebas de Application.
    public byte[] GetRowVersion(Npi npi) => npi.Id.ToByteArray();

    public void SetExpectedRowVersion(Npi npi, byte[] expectedRowVersion) { }
}

public sealed class FakeUnitOfWork : IUnitOfWork
{
    public int SaveChangesCallCount { get; private set; }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken)
    {
        SaveChangesCallCount++;
        return Task.FromResult(1);
    }
}
