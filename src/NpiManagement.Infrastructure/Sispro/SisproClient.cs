using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NpiManagement.Application.Exceptions;

namespace NpiManagement.Infrastructure.Sispro;

/// <summary>Lectura de SISPRO. Interfaz para poder probar el proveedor sin red.</summary>
public interface ISisproClient
{
    /// <summary>null si no existe. Lanza ExternalServiceUnavailableException si SISPRO no responde o no está configurado.</summary>
    Task<SisproCotizacionDto?> GetCotizacionAsync(string numero, CancellationToken cancellationToken);

    /// <summary>Prueba de conexión para TI: inicia sesión de cero. Lanza ExternalServiceUnavailableException si falla.</summary>
    Task CheckConnectionAsync(CancellationToken cancellationToken);
}

/// <summary>
/// Token de SISPRO compartido por todo el proceso. El HttpClient tipado se crea por petición, así
/// que el token no puede vivir en él: sin esto cada consulta haría su propio login.
/// </summary>
public sealed class SisproTokenStore
{
    internal SemaphoreSlim LoginLock { get; } = new(1, 1);
    internal string? Token { get; set; }
}

/// <summary>
/// Cliente de la API interna de SISPRO: POST /api/auth {Usuario, Password} devuelve un token que va
/// como "Authorization: Bearer" en las demás llamadas (es lo mismo que hace su propio front). El
/// token se guarda en memoria del proceso y se renueva al recibir 401. SOLO LECTURA: TI autorizó
/// leer; cualquier escritura en SISPRO requiere otra autorización.
/// </summary>
public sealed class SisproClient(HttpClient http, IOptions<SisproOptions> options, SisproTokenStore tokens, ILogger<SisproClient> logger) : ISisproClient
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    // Sin política de nombres: los campos salen tal cual se declaran (PascalCase).
    private static readonly JsonSerializerOptions LoginJson = new();

    private SisproOptions Options => options.Value;

    private string? CachedToken
    {
        get => tokens.Token;
        set => tokens.Token = value;
    }

    public async Task<SisproCotizacionDto?> GetCotizacionAsync(string numero, CancellationToken cancellationToken)
    {
        // El número va en la ruta: sólo dígitos, para no poder armar otra URL de SISPRO con él.
        if (string.IsNullOrWhiteSpace(numero) || !numero.Trim().All(char.IsAsciiDigit) || numero.Trim().Length > 9) return null;

        using var response = await SendAuthorizedAsync(() => new HttpRequestMessage(HttpMethod.Get, $"api/cotizacion/{numero.Trim()}"), cancellationToken);
        if (response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.BadRequest) return null;
        EnsureOk(response, "consultar la cotización");

        try
        {
            return await response.Content.ReadFromJsonAsync<SisproCotizacionDto>(Json, cancellationToken);
        }
        catch (JsonException ex)
        {
            // Respuesta que no se parece a una cotización: lo más probable es que SISPRO cambió su API.
            logger.LogError(ex, "SISPRO devolvió una cotización con formato inesperado (número {Numero}).", numero);
            throw new ExternalServiceUnavailableException("sispro-unexpected-response", "SISPRO respondió con un formato que el NPI no reconoce. Captura a mano y avisa a TI.", ex);
        }
    }

    public async Task CheckConnectionAsync(CancellationToken cancellationToken)
    {
        CachedToken = null;
        await LoginAsync(cancellationToken);
    }

    private async Task<HttpResponseMessage> SendAuthorizedAsync(Func<HttpRequestMessage> buildRequest, CancellationToken cancellationToken)
    {
        if (!Options.IsConfigured)
            throw new ExternalServiceUnavailableException("sispro-not-configured", "La conexión con SISPRO no está configurada en el servidor. Captura a mano o avisa a TI.");

        for (var attempt = 1; ; attempt++)
        {
            var token = CachedToken ?? await LoginAsync(cancellationToken);
            using var request = buildRequest();
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var response = await SendAsync(request, cancellationToken);
            if (response.StatusCode != HttpStatusCode.Unauthorized || attempt == 2) return response;

            // Token vencido: se pide uno nuevo una sola vez.
            response.Dispose();
            CachedToken = null;
        }
    }

    private async Task<string> LoginAsync(CancellationToken cancellationToken)
    {
        await tokens.LoginLock.WaitAsync(cancellationToken);
        try
        {
            if (CachedToken is not null) return CachedToken;

            // SISPRO (Node) distingue mayúsculas: espera exactamente "Usuario" y "Password", como su propio
            // front. JsonContent.Create usa por defecto camelCase ("usuario") y SISPRO rechazaba el login.
            using var request = new HttpRequestMessage(HttpMethod.Post, "api/auth")
            {
                Content = JsonContent.Create(new { Usuario = Options.Usuario, Password = Options.Password }, options: LoginJson),
            };
            using var response = await SendAsync(request, cancellationToken);
            if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.BadRequest or HttpStatusCode.Forbidden)
            {
                logger.LogError("SISPRO rechazó el usuario de la integración (HTTP {Status}).", (int)response.StatusCode);
                throw new ExternalServiceUnavailableException("sispro-login-rejected", "SISPRO rechazó el usuario configurado para el NPI (¿cambió la contraseña?). Captura a mano y avisa a TI.");
            }
            EnsureOk(response, "iniciar sesión");

            using var body = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(cancellationToken), cancellationToken: cancellationToken);
            if (!body.RootElement.TryGetProperty("token", out var tokenElement) || tokenElement.GetString() is not { Length: > 0 } token)
            {
                logger.LogError("La respuesta de login de SISPRO no trae token.");
                throw new ExternalServiceUnavailableException("sispro-unexpected-response", "SISPRO respondió con un formato que el NPI no reconoce. Captura a mano y avisa a TI.");
            }

            CachedToken = token;
            return token;
        }
        finally
        {
            tokens.LoginLock.Release();
        }
    }

    private async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        try
        {
            return await http.SendAsync(request, cancellationToken);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(ex, "SISPRO no respondió ({Method} {Path}).", request.Method, request.RequestUri);
            throw new ExternalServiceUnavailableException("sispro-unavailable", "SISPRO no respondió. Intenta de nuevo en un momento o captura a mano.", ex);
        }
    }

    private void EnsureOk(HttpResponseMessage response, string action)
    {
        if (response.IsSuccessStatusCode) return;
        logger.LogError("SISPRO devolvió HTTP {Status} al {Action}.", (int)response.StatusCode, action);
        throw new ExternalServiceUnavailableException("sispro-error", $"SISPRO devolvió un error al {action}. Intenta de nuevo o captura a mano.");
    }
}
