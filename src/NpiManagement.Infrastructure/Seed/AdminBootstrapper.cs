using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using NpiManagement.Domain;
using NpiManagement.Infrastructure.Identity;
using NpiManagement.Infrastructure.Persistence;

namespace NpiManagement.Infrastructure.Seed;

/// <summary>
/// Resuelve el problema del huevo y la gallina del auto-registro con aprobación: las solicitudes
/// sólo las puede aprobar un AdminTotal, pero fuera de Development no hay seeder que cree ninguno.
/// Sin esto, un despliegue nuevo se queda con una bandeja de pendientes que nadie puede atender.
///
/// A diferencia de <see cref="DevelopmentDataSeeder"/>, esto SÍ corre en Producción — por eso está
/// acotado por tres condiciones simultáneas:
///   1. Que <c>Bootstrap:AdminEmail</c> y <c>Bootstrap:AdminPassword</c> estén configurados.
///   2. Que no exista ya ningún usuario con rol AdminTotal (no toca nada si el sistema ya arrancó).
///   3. Que la contraseña cumpla la política de Identity — si no, falla ruidosamente en vez de
///      crear una cuenta de administrador con una contraseña débil.
///
/// Es de un solo uso por su propia naturaleza: en cuanto existe un AdminTotal, deja de hacer nada,
/// y conviene retirar las variables de configuración después del primer arranque.
/// </summary>
public static class AdminBootstrapper
{
    public static async Task EnsureInitialAdminAsync(
        NpiManagementDbContext dbContext,
        RoleManager<IdentityRole<Guid>> roleManager,
        UserManager<ApplicationUser> userManager,
        IConfiguration configuration,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        var email = configuration["Bootstrap:AdminEmail"];
        var password = configuration["Bootstrap:AdminPassword"];

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            return;

        // Los roles deben existir antes de poder asignar ninguno. En Development ya los creó el
        // seeder; en Producción esta puede ser la primera vez.
        foreach (var role in ApplicationRoles.All)
        {
            if (!await roleManager.RoleExistsAsync(role))
                await roleManager.CreateAsync(new IdentityRole<Guid>(role));
        }

        var existingAdmins = await userManager.GetUsersInRoleAsync(ApplicationRoles.AdminTotal);
        if (existingAdmins.Count > 0)
        {
            logger.LogInformation(
                "Bootstrap de administrador omitido: ya existen {Count} cuenta(s) con rol AdminTotal.",
                existingAdmins.Count);
            return;
        }

        var existing = await userManager.FindByEmailAsync(email);
        if (existing is not null)
        {
            // El correo ya tiene cuenta pero sin rol AdminTotal — probablemente se registró por la
            // vía normal antes de que hubiera quien lo aprobara. Se promueve en vez de duplicarlo.
            existing.Status = UserStatus.Active;
            existing.Area = AreaType.Total;
            existing.EmailConfirmed = true;
            existing.ApprovedAt = DateTimeOffset.UtcNow;
            await userManager.AddToRoleAsync(existing, ApplicationRoles.AdminTotal);
            await userManager.UpdateAsync(existing);

            logger.LogWarning(
                "Bootstrap: la cuenta {Email} ya existía y fue promovida a AdminTotal. " +
                "Retira Bootstrap:AdminEmail y Bootstrap:AdminPassword de la configuración.", email);
            return;
        }

        var admin = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            FullName = configuration["Bootstrap:AdminFullName"] ?? "Administrador",
            Area = AreaType.Total,
            Status = UserStatus.Active,
            RegisteredAt = DateTimeOffset.UtcNow,
            ApprovedAt = DateTimeOffset.UtcNow,
        };

        var created = await userManager.CreateAsync(admin, password);
        if (!created.Succeeded)
        {
            // Fallar el arranque es deliberado: una contraseña rechazada aquí significa que el
            // administrador inicial no existe, y un sistema sin administrador no puede operar.
            throw new InvalidOperationException(
                "No se pudo crear el administrador inicial: " +
                string.Join(" ", created.Errors.Select(e => e.Description)) +
                " Revisa Bootstrap:AdminPassword (mínimo 8 caracteres, con mayúscula, minúscula, dígito y símbolo).");
        }

        await userManager.AddToRoleAsync(admin, ApplicationRoles.AdminTotal);

        logger.LogWarning(
            "Bootstrap: creado el administrador inicial {Email}. Cambia su contraseña al primer " +
            "inicio de sesión y retira Bootstrap:AdminEmail y Bootstrap:AdminPassword de la configuración.",
            email);
    }
}
