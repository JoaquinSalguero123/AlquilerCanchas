using System.Security.Cryptography;
using AlquilerCanchas.Application.Interfaces;

namespace AlquilerCanchas.Infrastructure.Seguridad;

/// <summary>
/// PBKDF2-SHA256 con sal aleatoria por contraseña. Formato guardado: "PBKDF2$iteraciones$sal$hash"
/// (en Base64); guardar las iteraciones permite subirlas más adelante sin invalidar los hashes viejos.
/// </summary>
public class HasheadorPbkdf2 : IHasheadorDePasswords
{
    private const int TamanoSal = 16;
    private const int TamanoHash = 32;
    private const int Iteraciones = 210_000;

    public string Hashear(string password)
    {
        var sal = RandomNumberGenerator.GetBytes(TamanoSal);
        var hash = Rfc2898DeriveBytes.Pbkdf2(password, sal, Iteraciones, HashAlgorithmName.SHA256, TamanoHash);
        return $"PBKDF2${Iteraciones}${Convert.ToBase64String(sal)}${Convert.ToBase64String(hash)}";
    }

    public bool Verificar(string password, string hashGuardado)
    {
        var partes = hashGuardado.Split('$');
        if (partes.Length != 4 || partes[0] != "PBKDF2" || !int.TryParse(partes[1], out var iteraciones))
            return false;

        var sal = Convert.FromBase64String(partes[2]);
        var esperado = Convert.FromBase64String(partes[3]);
        var calculado = Rfc2898DeriveBytes.Pbkdf2(password, sal, iteraciones, HashAlgorithmName.SHA256, esperado.Length);

        // Comparación en tiempo constante: no filtra por timing cuántos bytes coincidieron.
        return CryptographicOperations.FixedTimeEquals(calculado, esperado);
    }
}
