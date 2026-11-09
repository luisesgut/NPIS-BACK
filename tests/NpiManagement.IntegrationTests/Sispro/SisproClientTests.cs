using System.Net;
using System.Text;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NpiManagement.Application.Exceptions;
using NpiManagement.Infrastructure.Sispro;
using Xunit;

namespace NpiManagement.IntegrationTests.Sispro;

/// <summary>Cliente de SISPRO contra un servidor simulado: login, token compartido, renovación y errores.</summary>
public class SisproClientTests
{
    /// <summary>Simula SISPRO: responde con la función dada y guarda lo que le pidieron.</summary>
    private sealed class FakeSispro(Func<HttpRequestMessage, int, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public List<(string Method, string Path, string? Authorization)> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add((request.Method.Method, request.RequestUri!.AbsolutePath, request.Headers.Authorization?.ToString()));
            return Task.FromResult(respond(request, Requests.Count(r => r.Path == request.RequestUri.AbsolutePath)));
        }
    }

    private static HttpResponseMessage Json(string json, HttpStatusCode status = HttpStatusCode.OK) =>
        new(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    private static HttpResponseMessage Login(int n) => Json($$"""{"token":"tok-{{n}}","Usuario":"npi"}""");

    private static (SisproClient Client, FakeSispro Server) Create(Func<HttpRequestMessage, int, HttpResponseMessage> respond, bool configured = true, SisproTokenStore? tokens = null)
    {
        var server = new FakeSispro(respond);
        var options = Options.Create(new SisproOptions { Usuario = configured ? "npi" : null, Password = configured ? "secreto" : null });
        var http = new HttpClient(server) { BaseAddress = new Uri("http://sispro.test/") };
        return (new SisproClient(http, options, tokens ?? new SisproTokenStore(), NullLogger<SisproClient>.Instance), server);
    }

    [Fact]
    public async Task Logs_in_once_and_reuses_the_token_across_requests()
    {
        var tokens = new SisproTokenStore();
        Func<HttpRequestMessage, int, HttpResponseMessage> respond = (req, n) =>
            req.RequestUri!.AbsolutePath == "/api/auth" ? Login(n) : Json(SisproFixtures.Cotizacion678);
        var (first, server1) = Create(respond, tokens: tokens);
        var (second, server2) = Create(respond, tokens: tokens);

        (await first.GetCotizacionAsync("678", CancellationToken.None))!.Codigo.Should().Be(678);
        (await second.GetCotizacionAsync("678", CancellationToken.None))!.Codigo.Should().Be(678);

        server1.Requests.Should().ContainSingle(r => r.Path == "/api/auth");
        server2.Requests.Should().NotContain(r => r.Path == "/api/auth", "el token es del proceso, no de cada HttpClient");
        server2.Requests.Single().Authorization.Should().Be("Bearer tok-1");
    }

    [Fact]
    public async Task The_login_sends_Usuario_and_Password_with_the_exact_casing_sispro_expects()
    {
        string? body = null;
        var (client, _) = Create((req, n) =>
        {
            if (req.RequestUri!.AbsolutePath != "/api/auth") return Json(SisproFixtures.Cotizacion678);
            body = req.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
            return Login(n);
        });

        await client.GetCotizacionAsync("678", CancellationToken.None);

        // SISPRO (Node) distingue mayúsculas: con "usuario"/"password" rechaza el login.
        body.Should().Be("""{"Usuario":"npi","Password":"secreto"}""");
    }

    [Fact]
    public async Task An_expired_token_is_renewed_once()
    {
        var (client, server) = Create((req, n) => req.RequestUri!.AbsolutePath switch
        {
            "/api/auth" => Login(n),
            _ when n == 1 => new HttpResponseMessage(HttpStatusCode.Unauthorized),
            _ => Json(SisproFixtures.Cotizacion678),
        });

        var quote = await client.GetCotizacionAsync("678", CancellationToken.None);

        quote.Should().NotBeNull();
        server.Requests.Count(r => r.Path == "/api/auth").Should().Be(2);
        server.Requests.Last().Authorization.Should().Be("Bearer tok-2");
    }

    [Fact]
    public async Task A_missing_quote_is_null_and_a_non_numeric_one_never_reaches_sispro()
    {
        var (client, server) = Create((req, n) => req.RequestUri!.AbsolutePath == "/api/auth" ? Login(n) : new HttpResponseMessage(HttpStatusCode.NotFound));

        (await client.GetCotizacionAsync("99999", CancellationToken.None)).Should().BeNull();
        (await client.GetCotizacionAsync("678/../usuarios", CancellationToken.None)).Should().BeNull();

        server.Requests.Should().NotContain(r => r.Path.Contains("usuarios"));
    }

    [Fact]
    public async Task Without_credentials_it_says_so_without_calling_sispro()
    {
        var (client, server) = Create((_, _) => throw new InvalidOperationException("no debería llamar"), configured: false);

        var act = () => client.GetCotizacionAsync("678", CancellationToken.None);

        (await act.Should().ThrowAsync<ExternalServiceUnavailableException>()).Which.Code.Should().Be("sispro-not-configured");
        server.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task Rejected_credentials_and_a_server_that_does_not_answer_become_unavailable()
    {
        var (rejected, _) = Create((_, _) => new HttpResponseMessage(HttpStatusCode.Unauthorized));
        var rejectedAct = () => rejected.GetCotizacionAsync("678", CancellationToken.None);
        (await rejectedAct.Should().ThrowAsync<ExternalServiceUnavailableException>()).Which.Code.Should().Be("sispro-login-rejected");

        var (down, _) = Create((_, _) => throw new HttpRequestException("connection refused"));
        var downAct = () => down.GetCotizacionAsync("678", CancellationToken.None);
        (await downAct.Should().ThrowAsync<ExternalServiceUnavailableException>()).Which.Code.Should().Be("sispro-unavailable");
    }
}
