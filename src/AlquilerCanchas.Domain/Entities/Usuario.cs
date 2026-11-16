using AlquilerCanchas.Domain.Enums;

namespace AlquilerCanchas.Domain.Entities;

/// <summary>
/// Personal administrativo que opera el sistema. NO figura en el diagrama de clases del TP1:
/// es la única entidad agregada, necesaria para que el login JWT tenga contra qué validar.
/// Los clientes no inician sesión (la minuta dice que la app es para el personal del complejo).
/// </summary>
public class Usuario : EntidadBase
{
    public string Email { get; set; } = string.Empty;

    /// <summary>Hash PBKDF2 con sal incluida; nunca la contraseña en claro.</summary>
    public string PasswordHash { get; set; } = string.Empty;

    public RolUsuario Rol { get; set; } = RolUsuario.Empleado;
}
