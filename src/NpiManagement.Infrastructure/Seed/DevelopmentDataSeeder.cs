using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using NpiManagement.Domain;
using NpiManagement.Infrastructure.Identity;
using NpiManagement.Infrastructure.Persistence;
using NpiManagement.Infrastructure.Persistence.Entities;

namespace NpiManagement.Infrastructure.Seed;

/// <summary>
/// Seed de roles, usuarios y datos mínimos — SÓLO para Development (se invoca condicionado a
/// env.IsDevelopment() desde Program.cs; nunca se ejecuta en otro entorno). Las contraseñas de
/// los usuarios semilla NUNCA quedan en texto plano en el repositorio: se toman de
/// configuración/User Secrets ("Seed:UsersPassword"); si no está configurada, se genera una
/// contraseña aleatoria de un solo uso y se imprime UNA vez en el log de arranque para que el
/// desarrollador pueda iniciar sesión — jamás "1234" ni ninguna contraseña real de producción.
/// </summary>
public static class DevelopmentDataSeeder
{
    public static readonly (string Email, string FullName, string Role, AreaType Area)[] Users =
    [
        ("evelazquez@bioflex.mx", "E. Velázquez", "AdminTotal", AreaType.Total),
        ("bbeas@bioflex.mx", "B. Beas", "AdminComercial", AreaType.Comercial),
        ("imartinez@bioflex.mx", "I. Martínez", "AdminIngenieria", AreaType.Ingenieria),
        ("schavez@bioflex.mx", "S. Chávez", "AdminDiseno", AreaType.Diseno),
        ("lvega@bioflex.mx", "Luis Vega", "UsuarioComercial", AreaType.Comercial),
        ("zgonzalez@bioflex.mx", "Z. González", "UsuarioIngenieria", AreaType.Ingenieria),
        ("mparedes@bioflex.mx", "M. Paredes", "UsuarioDiseno", AreaType.Diseno),
        ("sgomez@bioflex.mx", "Sandra Gómez", "UsuarioComercial", AreaType.Comercial),
        ("gquesada@bioflex.mx", "Godofredo Quesada", "UsuarioComercial", AreaType.Comercial),
        ("testevez@bioflex.mx", "T. Estévez", "UsuarioIngenieria", AreaType.Ingenieria),
    ];

    /// <summary>Fuente única: ApplicationRoles (Domain). Antes era una segunda lista literal que
    /// podía desincronizarse de las políticas declaradas en Program.cs.</summary>
    public static readonly string[] Roles = ApplicationRoles.All;

    private static readonly (string Numero, string Cliente, string TipoProducto, string Descripcion, string Ejecutivo, string Ancho, string PiFilm, string PiCal, string PplFilm, string PplCal, string PesoBobina, string TaponCore, string Piezas)[] Quotations =
    [
        ("1001", "CAYEF", "POUCH", "CAFÉ INSTANTÁNEO GOURMET", "LUIS VEGA", "273", "LDPE", "37", "CPP", "37", "N/A", "SI", "2000"),
        ("1002", "BABY CAT", "BOLSA", "BOLSA PARA ADITAMENTO BABY CAT", "SANDRA GOMEZ", "243", "LDPE OXO", "37.5", "N/A", "N/A", "N/A", "SI", "2500"),
        ("1003", "INDUSTRIAS GUACAMAYA", "BOBINA", "BOBINA LAMINADA PARA SELLAR VASOS", "LUIS VEGA", "190", "LDPE SUSTENTABLE", "36", "N/A", "N/A", "N/A", "NO", "N/A"),
        ("1004", "ELIDA LOPEZ GARCIA", "BOLSA EN ROLLO", "BOBINA METALIZADA FOTOCELDA PARA 1KG", "LUIS VEGA", "120", "COEXLDPE", "29", "N/A", "N/A", "N/A", "NO", "2000"),
        ("1005", "GRUPO BAZSTO", "BOLSA DE CAMISETA", "KER-092 1LB NATURES GOLD", "GODOFREDO QUESADA", "230", "CPP", "32", "LDPE", "32", "N/A", "SI", "3000"),
        ("1006", "KG DE MEXICO", "BOLSA EN ROLLO", "1980-0962-00 IPPOLITO QV 2CT ICEBERG LET (DESTINY)", "GODOFREDO QUESADA", "200", "LDPE OXO", "30", "LDPE", "N/A", "N/A", "SI", "2500"),
        ("1007", "VEGGIES FARM", "LAMINA", "BIOKOLLAG TEND SUPLEMENTO 20 G", "LUIS VEGA", "210", "CPP", "30", "LDPE", "N/A", "N/A", "SI", "3000"),
    ];

    private static readonly Dictionary<string, string[]> Catalogs = new()
    {
        ["FUELLES"] = ["LATERALES", "INFERIOR", "N/A"],
        ["TIPO DE MATERIAL"] = ["LDPE", "LDPE OXO", "LDPE SUSTENTABLE", "COEXLDPE", "COEXLDPE EVOH/PA", "BOPP", "BOPP CAVITADO", "BOPP OXO", "CPP", "CPP OXO", "PET", "BOPA(NY)", "N/A"],
        ["TERMINADO"] = ["TRANSPARENTE", "METALIZADO", "BLANCO", "MATE", "OTRO"],
        ["TERMINADO LAMINACION"] = ["N/A", "BAJO COF", "OXODEGRADABLE", "BAJO TIS", "CAVITADO", "ALTA BARRERA", "RETORTABLE", "ANTIFOG", "PELABLE", "ALOX", "SELLABLE"],
        ["TIPO DE PELICULA"] = ["BOPP", "CPP", "PET", "BOPA", "FOIL", "MDO", "PE"],
        ["TIPO EMPAQUE"] = ["PRIMARIO", "SECUNDARIO"],
        ["TIPO DE PRODUCTO"] = ["BOBINA", "BOLSA", "POUCH", "OTRO"],
        ["ACABADO"] = ["IMPRESO", "SIN IMPRESIÓN"],
        ["TIPO DE ENTRADA"] = ["PROTOTIPO/MUESTRA", "PRODUCTO NUEVO", "MODIFICACION", "OTRO"],
        ["CIERRE"] = ["ZIPPER", "ZIPPER MARIPOSA", "ZIPPER VELCRO", "N/A"],
        ["OTROS ELEMENTOS"] = ["SPOUT", "WICKET", "OTRO", "N/A"],
        ["PERFORACIONES"] = ["MACRO", "MICRO"],
        ["FORMATO"] = ["BOBINA PLANA", "BOBINA TUBULAR", "BOBINA LAMINADA", "BOLSA SELLO LATERAL", "BOLSA ZIPPER", "BOLSA WICKET", "STANDUP POUCH (SELLO K)", "DOYPACK (SELLO \"U\"/SONRISA)", "POUCH 2 SELLOS", "POUCH 3 SELLOS", "POUCH 4 SELLOS", "POUCH SELLO TRASLAPE", "POUCH SELLO ALETA", "OTRO"],
        ["FIGURA DE EMBOB"] = ["N/A", "1", "2", "3", "4", "5", "6", "7", "8"],
        ["CORE"] = ["3\"", "6\""],
        ["MERCADO"] = ["ALIMENTO LIQUIDO", "ALIMENTO SECO", "NO ALIMENTO LIQUIDO", "NO ALIMENTO SECO", "SECUNDARIO", "OTRO"],
        ["SUB MERCADO"] = ["ENDULZANTES", "GRANOS Y SEMILLAS", "CEREALES", "POLVOS", "CAFÉ", "REPOSTERIA", "PANADERIA", "TORTILLAS", "CONFITERIA", "BOTANAS", "FRUTAS Y VERDURAS", "LEGUMBRES", "ENCURTIDOS", "CARNES", "EMBUTIDOS", "QUESOS", "LACTEOS", "PROTEINA", "QUIMICOS", "ACEITES", "CUIDADO DEL HOGAR", "CUIDADO PERSONAL", "DESECHABLES", "FARMACEUTICO", "OTRO"],
        ["FORMULA LDPE"] = ["CONGELADO", "ALTO SELLO", "ALTA CLARIDAD", "COMERCIAL", "EMPAQUE AUTOMÁTICO", "LAMINACIÓN", "OTRO", "N/A"],
        ["COLGADERA"] = ["CIRCULAR", "SOMBRERO", "RIÑON", "N/A"],
        ["TIPO IMPRESIÓN"] = ["INTERNA (REVERSO)", "EXTERNA (FRENTE)", "N/A"],
        ["TIPO TINTA"] = ["LAMINACION", "FRENTE + BARNIZ", "FRENTE TERMORESISTENTE", "CONGELADO", "OTRA", "N/A"],
        ["SELLO"] = ["ALETA", "TRASLAPE", "LATERAL", "FONDO", "SELLO \"K\"", "SELLO \"U\"", "2 SELLOS", "3 SELLOS", "4 SELLOS", "N/A", "TOV", "OTRO"],
        ["SOLICITANTE"] = ["FERNANDO CAMARENA", "HECTOR PONS", "LUIS VEGA", "MANUEL DELGADILLO", "LILIANA GUTIERREZ", "JOAQUÍN COSÍO", "MARIA MARQUEZ", "RODRIGO MARTÍNEZ", "MOISES JIMENEZ", "DAYANA PUGA", "GUILLERMO DIEZ", "ALEXIA CRUZ", "MARIA PÉREZ", "DANIEL NAVARRO", "MONICA MARMOLEJO", "HUGO AGUIAR", "JESUS ARIZPE", "OTRO"],
        ["COSTO DE PREPRENSA"] = ["CLIENTE", "BIOFLEX"],
        ["CATEGORÍA SAP"] = ["BOPP MAP", "BOPP NATURAL / IMPRESO", "CINTA ACRILICA", "CPP ZIPPER", "STRECH", "LAMINADOS BOBINA BARRERA / VACIO (TAPA)", "LAMINADOS BOBINA ESTRUCTURAS", "LAMINADOS BOBINA ESTRUCTURAS DIGITAL", "LAMINADOS BOBINA ESTRUCTURAS DIGITAL TOP SEAL", "LAMINADOS BOBINA PE / PE", "LAMINADOS BOLSA STAND UP POUCH", "LAMINADOS BOLSA STAND UP POUCH DIGITAL", "PE BARRERA / VACIO", "PE BOBINA PLANA", "PE BOLSA SELLO LATERAL", "PE BOLSA WICKET", "PE BOLSA ZIPPER", "PE REYMA BOBINA Y BOLSA", "BOBINA PE / PE", "PE TERMOENCOGIBLE"],
        ["UNIDAD DE VENTA"] = ["MILL", "PZAS", "KG", "OTRO"],
    };

    public static async Task SeedAsync(
        NpiManagementDbContext dbContext,
        RoleManager<IdentityRole<Guid>> roleManager,
        UserManager<ApplicationUser> userManager,
        IConfiguration configuration,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        foreach (var role in Roles)
        {
            if (!await roleManager.RoleExistsAsync(role))
                await roleManager.CreateAsync(new IdentityRole<Guid>(role));
        }

        // La contraseña semilla nunca se genera ni se muestra automáticamente (ni en logs ni en
        // archivos): debe configurarse explícitamente vía User Secrets. Fallar rápido aquí, sin
        // revelar nada, es preferible a imprimir una contraseña "temporal" en la consola.
        var password = configuration["Seed:UsersPassword"];
        if (string.IsNullOrWhiteSpace(password))
        {
            throw new InvalidOperationException(
                "Falta configurar 'Seed:UsersPassword'. Ejecuta, dentro de src/NpiManagement.Api: " +
                "dotnet user-secrets set \"Seed:UsersPassword\" \"<una-contraseña>\" — ver docs/demo-setup.md. " +
                "Esta contraseña nunca se registra en logs ni se guarda en archivos versionados.");
        }
        logger.LogInformation("Sembrando usuarios de Development con la contraseña configurada en Seed:UsersPassword (no se registra su valor).");

        foreach (var (email, fullName, role, area) in Users)
        {
            if (await userManager.FindByEmailAsync(email) is not null) continue;

            var user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                FullName = fullName,
                Area = area,
                Status = UserStatus.Active,
                RegisteredAt = DateTimeOffset.UtcNow,
            };

            var createResult = await userManager.CreateAsync(user, password);
            if (!createResult.Succeeded)
            {
                // Nunca seguir en silencio: si Seed:UsersPassword no cumple la política de
                // Identity (longitud 8 + mayúscula + minúscula + dígito + carácter no
                // alfanumérico, por defecto), el síntoma visible sería un confuso "credenciales
                // inválidas" al hacer login mucho después, sin pista de la causa real.
                throw new InvalidOperationException(
                    $"No se pudo crear el usuario semilla '{email}': {string.Join("; ", createResult.Errors.Select(e => e.Description))}. " +
                    "Revisa que 'Seed:UsersPassword' cumpla la política de contraseñas de Identity (ver docs/demo-setup.md).");
            }

            await userManager.AddToRoleAsync(user, role);
        }

        if (!await dbContext.Quotations.AnyAsync(cancellationToken))
        {
            dbContext.Quotations.AddRange(Quotations.Select(q => new Quotation
            {
                Id = Guid.NewGuid(),
                NumeroCotizacion = q.Numero,
                Cliente = q.Cliente,
                TipoProducto = q.TipoProducto,
                Descripcion = q.Descripcion,
                DescripcionProducto = q.Descripcion,
                EjecutivoVenta = q.Ejecutivo,
                Ancho = q.Ancho,
                PiTipoPelicula = q.PiFilm,
                PiCalibre = q.PiCal,
                PplTipoPelicula = q.PplFilm,
                PplCalibre = q.PplCal,
                PslTipoPelicula = "N/A",
                PslCalibre = "N/A",
                PesoBobina = q.PesoBobina,
                TaponCore = q.TaponCore,
                PiezasCajaBulto = q.Piezas,
            }));
        }

        if (!await dbContext.Catalogs.AnyAsync(cancellationToken))
        {
            foreach (var (key, options) in Catalogs)
            {
                var catalog = new Catalog { Id = Guid.NewGuid(), Key = key, DisplayName = key };
                for (var i = 0; i < options.Length; i++)
                {
                    catalog.Options.Add(new CatalogOption
                    {
                        Id = Guid.NewGuid(),
                        CatalogId = catalog.Id,
                        Value = options[i],
                        DisplayOrder = i,
                        IsActive = true,
                    });
                }
                dbContext.Catalogs.Add(catalog);
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
