using FluentAssertions;
using NpiManagement.Application.Clients;
using NpiManagement.Application.Exceptions;
using Xunit;

namespace NpiManagement.Application.Tests.Clients;

public sealed class MergeClientNamesTests
{
    private sealed class FakeStore : IClientNameStore
    {
        public List<(IReadOnlyCollection<string> From, string To)> Renames { get; } = [];

        public Task<IReadOnlyList<ClientNameDto>> ListAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<ClientNameDto>>([]);

        public Task<int> RenameAsync(IReadOnlyCollection<string> from, string to, CancellationToken cancellationToken)
        {
            Renames.Add((from, to));
            return Task.FromResult(from.Count);
        }
    }

    [Fact]
    public async Task Unifica_los_nombres_distintos_al_correcto()
    {
        var store = new FakeStore();
        var count = await new MergeClientNamesCommandHandler(store)
            .HandleAsync(new MergeClientNamesCommand(["Mr Lucky", "MR. LUCKY", "Mr. Lucky", "Mr Lucky"], "  Mr. Lucky "), default);

        // El nombre unificado queda en MAYÚSCULAS: "MR. LUCKY" ya está bien y no se toca.
        count.Should().Be(2);
        store.Renames.Should().ContainSingle();
        store.Renames[0].To.Should().Be("MR. LUCKY");
        store.Renames[0].From.Should().BeEquivalentTo(["Mr Lucky", "Mr. Lucky"]);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Exige_el_nombre_correcto(string to)
    {
        var act = () => new MergeClientNamesCommandHandler(new FakeStore()).HandleAsync(new MergeClientNamesCommand(["Mr Lucky"], to), default);
        await act.Should().ThrowAsync<RequestValidationException>();
    }

    [Fact]
    public async Task Sin_nombres_por_cambiar_no_hace_nada()
    {
        var store = new FakeStore();
        var act = () => new MergeClientNamesCommandHandler(store).HandleAsync(new MergeClientNamesCommand(["MR. LUCKY"], "Mr. Lucky"), default);
        await act.Should().ThrowAsync<RequestValidationException>();
        store.Renames.Should().BeEmpty();
    }

    [Fact]
    public async Task Rechaza_un_nombre_mas_largo_que_la_columna()
    {
        var act = () => new MergeClientNamesCommandHandler(new FakeStore())
            .HandleAsync(new MergeClientNamesCommand(["Mr Lucky"], new string('A', MergeClientNamesCommandHandler.MaxLength + 1)), default);
        await act.Should().ThrowAsync<RequestValidationException>();
    }
}
