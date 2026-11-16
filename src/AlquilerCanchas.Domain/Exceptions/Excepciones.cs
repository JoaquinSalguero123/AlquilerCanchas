namespace AlquilerCanchas.Domain.Exceptions;

/// <summary>Base de los errores de negocio; la WebAPI los traduce a códigos HTTP.</summary>
public abstract class ExcepcionDeNegocio : Exception
{
    protected ExcepcionDeNegocio(string mensaje) : base(mensaje) { }
}

/// <summary>El recurso pedido no existe → 404.</summary>
public class NoEncontradoException : ExcepcionDeNegocio
{
    public NoEncontradoException(string entidad, object id)
        : base($"No existe {entidad} con id {id}.") { }
}

/// <summary>Regla de negocio violada (superposición, transición inválida, sobrepago) → 409.</summary>
public class ReglaDeNegocioException : ExcepcionDeNegocio
{
    public ReglaDeNegocioException(string mensaje) : base(mensaje) { }
}

/// <summary>Dato de entrada inválido → 400.</summary>
public class ValidacionException : ExcepcionDeNegocio
{
    public ValidacionException(string mensaje) : base(mensaje) { }
}

/// <summary>Credenciales incorrectas → 401.</summary>
public class CredencialesInvalidasException : ExcepcionDeNegocio
{
    public CredencialesInvalidasException() : base("Email o contraseña incorrectos.") { }
}

/// <summary>Un servicio de terceros no respondió o devolvió un error → 503.</summary>
public class ServicioExternoNoDisponibleException : ExcepcionDeNegocio
{
    public ServicioExternoNoDisponibleException(string servicio, Exception? causa = null)
        : base($"El servicio externo {servicio} no está disponible en este momento. Probá de nuevo más tarde.")
    {
        if (causa is not null) Data["causa"] = causa.Message;
    }
}
