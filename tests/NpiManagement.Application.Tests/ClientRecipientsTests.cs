using FluentAssertions;
using NpiManagement.Application.ClientRecipients;
using NpiManagement.Application.Exceptions;
using Xunit;

namespace NpiManagement.Application.Tests;

/// <summary>Libreta de correos por cliente para el borrador de Outlook (2026-11-09).</summary>
public class ClientRecipientsTests
{
    private sealed class MemoryStore : IClientRecipientStore
    {
        public readonly Dictionary<(Guid, string), IReadOnlyList<ClientRecipientDto>> Data = new();
        public Task<IReadOnlyList<ClientRecipientDto>> ListAsync(Guid userId, string cliente, CancellationToken cancellationToken) =>
            Task.FromResult(Data.TryGetValue((userId, cliente), out var list) ? list : (IReadOnlyList<ClientRecipientDto>)[]);
        public Task ReplaceAsync(Guid userId, string cliente, IReadOnlyList<ClientRecipientDto> recipients, CancellationToken cancellationToken)
        {
            Data[(userId, cliente)] = recipients;
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task Guarda_por_vendedor_y_cliente_en_mayusculas_sin_repetidos()
    {
        var store = new MemoryStore();
        var user = Guid.NewGuid();
        await new ClientRecipientsService(store).SaveAsync(user, " Destiny Packaging ",
            [new(" Compras@Destiny.com ", "To", true), new("compras@destiny.com", "Cc", false), new("calidad@destiny.com", "cc", true)], default);

        store.Data[(user, "DESTINY PACKAGING")].Should().Equal(
            new ClientRecipientDto("compras@destiny.com", "To", true),
            new ClientRecipientDto("calidad@destiny.com", "Cc", true));
    }

    [Theory]
    [InlineData("no-es-correo")]
    [InlineData("a@b")]
    [InlineData("dos@uno.com; otro@x.com")]
    public void Rechaza_correos_invalidos(string email)
    {
        var act = () => ClientRecipientsService.Clean([new(email, "To", true)]);
        act.Should().Throw<RequestValidationException>().Which.Code.Should().Be("recipient-email-invalid");
    }
}
