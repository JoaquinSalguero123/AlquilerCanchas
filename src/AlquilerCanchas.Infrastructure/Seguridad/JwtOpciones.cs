namespace AlquilerCanchas.Infrastructure.Seguridad;

/// <summary>
/// Sección "Jwt" de la configuración. La Key NO va en appsettings.json: en desarrollo sale de
/// user-secrets y en Azure de una variable de entorno / Key Vault (secreto "Jwt--Key").
/// </summary>
public class JwtOpciones
{
    public const string Seccion = "Jwt";
    public const int LargoMinimoDeKey = 32;

    public string Key { get; set; } = string.Empty;
    public string Issuer { get; set; } = "AlquilerCanchas.API";
    public string Audience { get; set; } = "AlquilerCanchas.Clientes";
    public int ExpiraMinutos { get; set; } = 60;
}
