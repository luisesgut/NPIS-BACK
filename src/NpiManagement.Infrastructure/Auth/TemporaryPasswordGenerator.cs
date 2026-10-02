using System.Security.Cryptography;

namespace NpiManagement.Infrastructure.Auth;

/// <summary>
/// Contraseñas temporales que un administrador le comparte a alguien, a veces dictándolas. Sin
/// caracteres que se confunden al leerlos (0/O, 1/l/I) y siempre con mayúscula, minúscula, dígito y
/// símbolo para cumplir la política de Identity. Aleatoriedad criptográfica: mientras dura, es una
/// credencial real.
/// </summary>
public static class TemporaryPasswordGenerator
{
    public const int Length = 12;

    private const string Upper = "ABCDEFGHJKLMNPQRSTUVWXYZ";
    private const string Lower = "abcdefghijkmnpqrstuvwxyz";
    private const string Digits = "23456789";
    private const string Symbols = "!@#$%*?";

    public static string Create()
    {
        var all = Upper + Lower + Digits + Symbols;
        var chars = new char[Length];

        // Uno de cada tipo garantizado; el resto al azar del conjunto completo, y luego se revuelve
        // para que la posición de cada tipo no sea predecible.
        chars[0] = Pick(Upper);
        chars[1] = Pick(Lower);
        chars[2] = Pick(Digits);
        chars[3] = Pick(Symbols);
        for (var i = 4; i < Length; i++)
            chars[i] = Pick(all);

        RandomNumberGenerator.Shuffle(chars.AsSpan());
        return new string(chars);
    }

    private static char Pick(string set) => set[RandomNumberGenerator.GetInt32(set.Length)];
}
