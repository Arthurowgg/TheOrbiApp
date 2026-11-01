using System.Text;

namespace Orbi.Services;

/// <summary>
/// Códigos de vínculo no formato ORBI-XXXX-XXXX, com dígito verificador.
/// O mesmo algoritmo roda no site (src/lib/accounts.ts), então o site
/// recusa erro de digitação antes de falar com o Firestore.
/// </summary>
public static class PairingCodes
{
    private const string Alphabet = "23456789ABCDEFGHJKMNPQRSTUVWXYZ";

    private static char CheckChar(string body)
    {
        var sum = 0;
        for (var i = 0; i < body.Length; i++)
        {
            sum += (i + 1) * Alphabet.IndexOf(body[i], StringComparison.Ordinal);
        }
        return Alphabet[sum % Alphabet.Length];
    }

    public static string Generate()
    {
        var body = new StringBuilder(7);
        for (var i = 0; i < 7; i++)
        {
            body.Append(Alphabet[Random.Shared.Next(Alphabet.Length)]);
        }
        var full = body + CheckChar(body.ToString());
        return $"ORBI-{full[..4]}-{full[4..]}";
    }

    public static bool IsValid(string code)
    {
        var clean = new string(code.ToUpperInvariant().Where(char.IsLetterOrDigit).ToArray());
        if (clean.StartsWith("ORBI", StringComparison.Ordinal)) clean = clean[4..];
        if (clean.Length != 8) return false;
        if (clean.Any(ch => !Alphabet.Contains(ch))) return false;
        return CheckChar(clean[..7]) == clean[7];
    }
}
