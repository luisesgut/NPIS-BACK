namespace NpiManagement.Domain;

/// <summary>
/// Los 7 roles funcionales del sistema. Viven como roles nativos de ASP.NET Identity
/// (docs/domain-model.md §8), pero sus nombres se declaran aquí para que exista una única fuente
/// de verdad: hasta ahora estaban repetidos como literales en Program.cs (políticas) y en
/// DevelopmentDataSeeder (siembra), sin nada que garantizara que ambas listas coincidieran.
/// </summary>
public static class ApplicationRoles
{
    public const string AdminTotal = "AdminTotal";
    public const string AdminComercial = "AdminComercial";
    public const string AdminIngenieria = "AdminIngenieria";
    public const string AdminDiseno = "AdminDiseno";
    public const string UsuarioComercial = "UsuarioComercial";
    public const string UsuarioIngenieria = "UsuarioIngenieria";
    public const string UsuarioDiseno = "UsuarioDiseno";

    public static readonly string[] All =
        [AdminTotal, AdminComercial, AdminIngenieria, AdminDiseno, UsuarioComercial, UsuarioIngenieria, UsuarioDiseno];

    /// <summary>
    /// Área que corresponde a cada rol. La autorización de todo el workflow se apoya en la pareja
    /// (rol, área), así que una combinación incoherente — un AdminComercial con área Diseño —
    /// produciría un usuario con permisos impredecibles. Por eso la aprobación valida contra esto
    /// en vez de confiar en lo que venga en la petición.
    /// </summary>
    private static readonly Dictionary<string, AreaType> AreaByRole = new(StringComparer.Ordinal)
    {
        [AdminTotal] = AreaType.Total,
        [AdminComercial] = AreaType.Comercial,
        [AdminIngenieria] = AreaType.Ingenieria,
        [AdminDiseno] = AreaType.Diseno,
        [UsuarioComercial] = AreaType.Comercial,
        [UsuarioIngenieria] = AreaType.Ingenieria,
        [UsuarioDiseno] = AreaType.Diseno,
    };

    public static bool Exists(string role) => AreaByRole.ContainsKey(role);

    /// <summary>El área que impone el rol, o null si el rol no existe.</summary>
    public static AreaType? AreaFor(string role) =>
        AreaByRole.TryGetValue(role, out var area) ? area : null;

    public static bool IsAdmin(string role) => role.StartsWith("Admin", StringComparison.Ordinal);
}
