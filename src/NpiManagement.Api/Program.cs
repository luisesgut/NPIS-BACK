using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpLogging;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using NpiManagement.Api;
using NpiManagement.Api.Realtime;
using NpiManagement.Application;
using NpiManagement.Application.Abstractions;
using NpiManagement.Infrastructure;
using NpiManagement.Infrastructure.Auth;
using NpiManagement.Infrastructure.Identity;
using NpiManagement.Infrastructure.Persistence;
using NpiManagement.Infrastructure.Seed;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers()
    .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
// Registra el esquema de seguridad Bearer en el documento para que Swagger UI muestre el botón
// "Authorize" (sin esto, AddOpenApi() por sí solo no sabe que la API usa JWT).
builder.Services.AddOpenApi(options =>
{
    options.AddDocumentTransformer((document, _, _) =>
    {
        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes["Bearer"] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            In = ParameterLocation.Header,
            Description = "Pega sólo el access token — sin el prefijo 'Bearer '.",
        };
        document.SecurityRequirements.Add(new OpenApiSecurityRequirement
        {
            [new OpenApiSecurityScheme { Reference = new OpenApiReference { Id = "Bearer", Type = ReferenceType.SecurityScheme } }] = [],
        });
        return Task.CompletedTask;
    });
});

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddApplication();

// Chat en tiempo real (NpiChatHub). Un solo servidor IIS: no hace falta backplane.
// CloseOnAuthenticationExpiration: una conexión WebSocket abierta no vuelve a validar el token por
// sí sola; así, al vencer el access token (15 min) se cierra y el cliente reconecta con uno nuevo —
// una cuenta desactivada no se queda escuchando indefinidamente.
builder.Services.AddSignalR()
    .AddJsonProtocol(options => options.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddSingleton<Microsoft.AspNetCore.SignalR.IUserIdProvider, UserIdFromTokenProvider>();
builder.Services.AddScoped<IChatRealtime, SignalRChatRealtime>();

builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services.AddProblemDetails();

// Logging de requests deliberadamente mínimo: método, ruta, código y duración. NUNCA se incluyen
// encabezados (el Authorization Bearer y las cookies viajan ahí) ni cuerpos de request/response
// (contendrían credenciales de login, refresh tokens o el binario de los adjuntos subidos).
builder.Services.AddHttpLogging(options =>
{
    options.LoggingFields = HttpLoggingFields.RequestMethod | HttpLoggingFields.RequestPath |
        HttpLoggingFields.ResponseStatusCode | HttpLoggingFields.Duration;
});

// CORS: sólo los orígenes explícitamente configurados (Cors:AllowedOrigins) pueden llamar a la
// API desde un navegador. Sin credenciales de cookie (la sesión viaja como Bearer token en el
// encabezado Authorization, no en una cookie), así que no se necesita AllowCredentials.
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(options =>
{
    options.AddPolicy("Default", policy =>
    {
        if (allowedOrigins.Length > 0)
            policy.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod();
    });
});

// Límite de intentos de login/refresh por IP — defensa adicional a la que ya aplica ASP.NET Core
// Identity por cuenta (Lockout, ver InfrastructureDependencyInjection): esto también protege
// contra un mismo IP probando muchas cuentas distintas, no sólo una cuenta muchas veces.
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("auth", httpContext => RateLimitPartition.GetFixedWindowLimiter(
        partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        factory: _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 10,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
        }));

    // Reportes desde la app, por usuario: holgado para el uso normal, pero corta un bucle (una
    // pantalla que truena en cada render y alguien pulsando "Reportar" una y otra vez) antes de
    // llenar de capturas la carpeta de adjuntos.
    options.AddPolicy("feedback", httpContext => RateLimitPartition.GetFixedWindowLimiter(
        partitionKey: httpContext.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
            ?? httpContext.Connection.RemoteIpAddress?.ToString()
            ?? "unknown",
        factory: _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 10,
            Window = TimeSpan.FromMinutes(10),
            QueueLimit = 0,
        }));
});

// AddIdentity (en AddInfrastructure) registra su propio esquema de cookies y lo deja como
// DefaultAuthenticateScheme/DefaultChallengeScheme. El overload de un solo string de
// AddAuthentication sólo pisa DefaultScheme, no esos dos — así que sin esto, cada request
// terminaba autenticándose (y desafiándose en un 401) contra la cookie de Identity en vez del
// Bearer: nunca se llegaba a leer el header Authorization. Hay que fijar los tres explícitamente.
builder.Services
    .AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer();

// La configuración de TokenValidationParameters se resuelve vía IOptions<JwtOptions> — el mismo
// singleton que JwtTokenService usa para FIRMAR tokens (registrado en AddInfrastructure con
// services.Configure<JwtOptions>(...)). Antes se leía dos veces por separado (aquí directo de
// builder.Configuration antes de construir el contenedor, y en JwtTokenService vía IOptions):
// si el archivo de secretos cambiaba mientras el proceso seguía vivo, cada lectura podía capturar
// un valor distinto y firmar/validar con claves diferentes — 401 en toda request tras un login
// que sí emitía un token "válido". Compartir la misma fuente elimina esa clase de bug de raíz.
builder.Services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
    .Configure<IOptions<JwtOptions>>((bearerOptions, jwtOptionsAccessor) =>
    {
        var jwtOptions = jwtOptionsAccessor.Value;
        if (string.IsNullOrWhiteSpace(jwtOptions.SigningKey))
            throw new InvalidOperationException("Falta la sección de configuración 'Jwt' (Issuer/Audience/SigningKey). Ver .env.example.");

        bearerOptions.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtOptions.Issuer,
            ValidateAudience = true,
            ValidAudience = jwtOptions.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SigningKey)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30),
            RoleClaimType = System.Security.Claims.ClaimTypes.Role,
            NameClaimType = System.Security.Claims.ClaimTypes.Name,
        };

        // El navegador no puede mandar el header Authorization al abrir un WebSocket, así que
        // SignalR manda el token en ?access_token=. Sólo se acepta en /hubs: en el resto de la API
        // el token sigue llegando únicamente por header (un token en la URL acaba en logs).
        bearerOptions.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                if (!string.IsNullOrEmpty(accessToken) && context.HttpContext.Request.Path.StartsWithSegments("/hubs"))
                    context.Token = accessToken;
                return Task.CompletedTask;
            },
        };
    });

// Políticas por rol (docs/authorization-matrix.md). Las políticas dependientes del recurso
// (propiedad/asignación de un Npi concreto) viven en Application.Authorization.NpiAuthorization
// y se aplican dentro de cada caso de uso (Fase 4: NpisController) — no son un
// [Authorize(Policy=...)] declarativo porque dependen de datos del recurso, no sólo del rol.
builder.Services.AddAuthorizationBuilder()
    .AddPolicy("AdminTotal", p => p.RequireRole("AdminTotal"))
    .AddPolicy("AnyAdmin", p => p.RequireRole("AdminTotal", "AdminComercial", "AdminIngenieria", "AdminDiseno"))
    .AddPolicy("AreaComercial", p => p.RequireRole("AdminTotal", "AdminComercial", "UsuarioComercial"))
    .AddPolicy("AreaIngenieria", p => p.RequireRole("AdminTotal", "AdminIngenieria", "UsuarioIngenieria"))
    .AddPolicy("AreaDiseno", p => p.RequireRole("AdminTotal", "AdminDiseno", "UsuarioDiseno"))
    .AddPolicy("QuotationsRead", p => p.RequireRole("AdminTotal", "AdminComercial", "AdminIngenieria", "AdminDiseno", "UsuarioComercial"));

var app = builder.Build();

// Migraciones automáticas: apagadas por defecto (Database:ApplyMigrationsOnStartup=false —
// ver .env.example y docs/demo-setup.md, donde se aplican a mano con `dotnet ef database
// update`). Incluso si algún día ese valor llegara en true por error de configuración, el
// `IsDevelopment()` es una segunda condición obligatoria: fuera de Development esta rama nunca
// se ejecuta, sin excepción — nunca se migra automáticamente en Production.
if (app.Environment.IsDevelopment() && builder.Configuration.GetValue("Database:ApplyMigrationsOnStartup", false))
{
    using var migrationScope = app.Services.CreateScope();
    var dbContext = migrationScope.ServiceProvider.GetRequiredService<NpiManagementDbContext>();
    await dbContext.Database.MigrateAsync();
}

// Siembra de roles/usuarios/catálogos mínimos — sólo Development, independiente de si las
// migraciones se aplicaron automáticamente o a mano; requiere que el esquema ya exista.
if (app.Environment.IsDevelopment())
{
    using var seedScope = app.Services.CreateScope();
    var dbContext = seedScope.ServiceProvider.GetRequiredService<NpiManagementDbContext>();
    var roleManager = seedScope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
    var userManager = seedScope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
    var configuration = seedScope.ServiceProvider.GetRequiredService<IConfiguration>();
    var logger = seedScope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    await DevelopmentDataSeeder.SeedAsync(dbContext, roleManager, userManager, configuration, logger);
}

// Administrador inicial. A diferencia del seeder de arriba, esto SÍ corre en Producción: sin un
// AdminTotal no hay quién apruebe las solicitudes de acceso y el sistema queda inservible. No
// hace nada salvo que Bootstrap:AdminEmail/AdminPassword estén configurados Y no exista ya un
// AdminTotal — ver AdminBootstrapper.
{
    using var bootstrapScope = app.Services.CreateScope();
    var dbContext = bootstrapScope.ServiceProvider.GetRequiredService<NpiManagementDbContext>();
    var roleManager = bootstrapScope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
    var userManager = bootstrapScope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
    var configuration = bootstrapScope.ServiceProvider.GetRequiredService<IConfiguration>();
    var logger = bootstrapScope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    await AdminBootstrapper.EnsureInitialAdminAsync(dbContext, roleManager, userManager, configuration, logger);
}

app.UseExceptionHandler();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/openapi/v1.json", "NpiManagement API v1");
        options.RoutePrefix = "swagger";
    });
}
else
{
    app.UseHsts();
}

app.UseHttpLogging();

// Encabezados de endurecimiento aplicados a toda respuesta. No hay contenido embebible en un
// <iframe> ni tipos MIME "adivinados" por el navegador; la API nunca necesita enviar Referer a
// otro origen.
app.Use(async (context, next) =>
{
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers["X-Frame-Options"] = "DENY";
    context.Response.Headers["Referrer-Policy"] = "no-referrer";
    context.Response.Headers["Permissions-Policy"] = "geolocation=(), camera=(), microphone=()";
    await next();
});

app.UseHttpsRedirection();

app.UseCors("Default");

app.UseAuthentication();

// Cuenta con contraseña temporal: hasta que la cambie sólo puede usar /api/auth (cambiarla, ver su
// sesión, cerrarla). El frontend ya muestra únicamente la pantalla de cambio, pero la regla vive
// aquí para no depender de él.
app.Use(async (context, next) =>
{
    if (context.User.HasClaim(c => c.Type == AuthClaims.MustChangePassword) &&
        !context.Request.Path.StartsWithSegments("/api/auth"))
    {
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        await context.Response.WriteAsJsonAsync(new ProblemDetails
        {
            Status = StatusCodes.Status403Forbidden,
            Title = "Debes cambiar tu contraseña temporal antes de continuar.",
            Type = "password-change-required",
        });
        return;
    }

    await next();
});

app.UseAuthorization();

app.UseRateLimiter();

app.MapControllers();
app.MapHub<NpiChatHub>(NpiChatHub.Path, options => options.CloseOnAuthenticationExpiration = true);

var healthCheckOptions = new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions { ResponseWriter = WriteHealthReportAsJson };
app.MapHealthChecks("/health", healthCheckOptions);
app.MapHealthChecks("/health/ready", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready"),
    ResponseWriter = WriteHealthReportAsJson,
});

app.Run();

/// <summary>Detalle por chequeo (nombre, estado, duración, mensaje de la excepción si falló) — sin
/// esto, el body por defecto es solo el texto plano "Healthy"/"Unhealthy", sin decir cuál chequeo
/// falló ni por qué.</summary>
static Task WriteHealthReportAsJson(HttpContext context, Microsoft.Extensions.Diagnostics.HealthChecks.HealthReport report)
{
    context.Response.ContentType = "application/json";
    var payload = new
    {
        status = report.Status.ToString(),
        totalDurationMs = report.TotalDuration.TotalMilliseconds,
        checks = report.Entries.Select(e => new
        {
            name = e.Key,
            status = e.Value.Status.ToString(),
            durationMs = e.Value.Duration.TotalMilliseconds,
            description = e.Value.Description,
            error = e.Value.Exception?.Message,
        }),
    };
    return context.Response.WriteAsync(JsonSerializer.Serialize(payload));
}

// Necesario para que WebApplicationFactory<Program> (pruebas de integración) pueda referenciar
// este ensamblado de entrada.
public partial class Program;
