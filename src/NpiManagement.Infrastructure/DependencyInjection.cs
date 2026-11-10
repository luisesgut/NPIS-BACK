using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NpiManagement.Application.Abstractions;
using NpiManagement.Domain.Common;
using NpiManagement.Infrastructure.Auth;
using NpiManagement.Infrastructure.Common;
using NpiManagement.Infrastructure.Email;
using NpiManagement.Infrastructure.FileStorage;
using NpiManagement.Infrastructure.Identity;
using NpiManagement.Infrastructure.Notifications;
using NpiManagement.Infrastructure.Persistence;
using NpiManagement.Infrastructure.Persistence.Repositories;
using NpiManagement.Infrastructure.Services;
using NpiManagement.Infrastructure.Sispro;

namespace NpiManagement.Infrastructure;

/// <summary>Composición de dependencias de Infrastructure (ver docs/architecture-decisions.md).</summary>
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException(
                "Falta la cadena de conexión 'ConnectionStrings:DefaultConnection'. Configúrala en appsettings, " +
                "variables de entorno o User Secrets — nunca con credenciales reales en el repositorio.");

        services.AddDbContext<NpiManagementDbContext>(options =>
            options.UseSqlServer(connectionString, sql => sql.MigrationsAssembly(typeof(NpiManagementDbContext).Assembly.FullName)));

        services.AddIdentity<ApplicationUser, IdentityRole<Guid>>(options =>
            {
                // Bloqueo por intentos fallidos (requisito de Fase 3; se configura aquí porque
                // Identity se registra en esta fase, aunque el login/JWT llega después).
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
                options.Lockout.AllowedForNewUsers = true;
                options.Password.RequiredLength = 8;
            })
            .AddEntityFrameworkStores<NpiManagementDbContext>()
            .AddDefaultTokenProviders();

        services.AddScoped<IClock, SystemClock>();
        services.AddSingleton<IBusinessCalendar, MexicoBusinessCalendar>();
        services.AddScoped<INpiNumberGenerator, SqlServerNpiNumberGenerator>();
        // Cotizaciones: se leen de SISPRO (solo lectura). Sin credenciales en Sispro:* la consulta
        // responde 503 "no configurado" y el NPI se captura a mano.
        services.Configure<SisproOptions>(configuration.GetSection(SisproOptions.SectionName));
        Domain.SisproQuoteGate.Configured = configuration.GetSection(SisproOptions.SectionName).GetValue<bool>(nameof(SisproOptions.CotizacionObligatoria));
        services.AddHostedService<SisproQuoteWaitRelease>();
        services.AddSingleton<SisproTokenStore>();
        services.AddHttpClient<ISisproClient, SisproClient>((sp, http) =>
        {
            var sispro = configuration.GetSection(SisproOptions.SectionName).Get<SisproOptions>() ?? new SisproOptions();
            http.BaseAddress = new Uri(sispro.BaseUrl.TrimEnd('/') + "/");
            http.Timeout = TimeSpan.FromSeconds(Math.Clamp(sispro.TimeoutSeconds, 3, 60));
        });
        services.AddScoped<IQuotationProvider, SisproQuotationProvider>();
        services.AddScoped<ISisproAdminService, SisproAdminService>();
        services.AddScoped<INpiRepository, NpiRepository>();
        services.AddScoped<INpiDeletion, EfNpiDeletion>();
        services.AddScoped<NpiManagement.Application.Npis.Turns.INpiTurnReader, EfNpiTurnReader>();
        services.AddScoped<INpiListReader, EfNpiListReader>();
        services.AddScoped<IDashboardReader, EfDashboardReader>();
        services.AddScoped<NpiManagement.Application.Dashboard.IDashboardOverviewReader, EfDashboardOverviewReader>();
        services.AddScoped<NpiManagement.Application.Clients.IClientNameStore, EfClientNameStore>();
        services.AddScoped<IUserDirectory, EfUserDirectory>();
        services.AddScoped<IUserAdminService, UserAdminService>();
        services.AddScoped<ICatalogReader, EfCatalogReader>();
        services.AddScoped<ICatalogAdminService, EfCatalogAdminService>();
        services.AddScoped<IBobinaCatalogStore, EfBobinaCatalogStore>();
        services.AddScoped<INotificationService, EfNotificationService>();
        services.AddScoped<NpiManagement.Application.ClientRecipients.IClientRecipientStore, EfClientRecipientStore>();
        services.AddMemoryCache();
        services.AddScoped<NpiManagement.Application.Users.IClientViewGrantStore, EfClientViewGrantStore>();
        services.AddScoped<INpiChatStore, EfNpiChatStore>();
        services.AddScoped<INpiCaptureHistoryStore, EfNpiCaptureHistoryStore>();
        services.AddScoped<IFeedbackService, FeedbackService>();
        services.AddScoped<IUnitOfWork, EfUnitOfWork>();
        // La API lo reemplaza por SignalR; aquí queda el "sin tiempo real" por si se usa Infrastructure sin la API.
        services.TryAddSingleton<IRealtimeEvents, NullRealtimeEvents>();

        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));
        services.AddScoped<IJwtTokenService, JwtTokenService>();
        services.AddScoped<IAuthService, AuthService>();

        services.Configure<FileStorageOptions>(configuration.GetSection(FileStorageOptions.SectionName));
        services.AddSingleton<IFileStorage, LocalFileStorage>();
        services.AddSingleton<IAttachmentPolicy, ConfiguredAttachmentPolicy>();

        // Correo. El buffer es por petición; la cola y el worker son del proceso entero. Con
        // Mail:Enabled=false se registra NullEmailSender y el sistema queda idéntico a como estaba
        // antes de que existiera el correo — no hay una segunda ruta de código que mantener.
        services.Configure<MailOptions>(configuration.GetSection(MailOptions.SectionName));
        services.AddScoped<PendingEmailNotifications>();
        services.AddSingleton<IEmailDispatchQueue, ChannelEmailDispatchQueue>();
        services.AddHostedService<EmailDispatchWorker>();

        if (configuration.GetValue("Mail:Enabled", false))
            services.AddScoped<IEmailSender, SmtpEmailSender>();
        else
            services.AddScoped<IEmailSender, NullEmailSender>();

        services.AddHealthChecks()
            .AddCheck<SqlServerHealthCheck>("sql-server", tags: ["ready"])
            .AddCheck<FileStorageHealthCheck>("file-storage", tags: ["ready"]);

        return services;
    }
}
