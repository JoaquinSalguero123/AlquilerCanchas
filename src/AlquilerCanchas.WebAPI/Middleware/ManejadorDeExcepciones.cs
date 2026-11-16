using AlquilerCanchas.Domain.Exceptions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace AlquilerCanchas.WebAPI.Middleware;

/// <summary>
/// Traduce las excepciones de negocio a códigos HTTP en un solo lugar, así los controladores no
/// llevan try/catch y todos los errores salen con el mismo formato (ProblemDetails, RFC 9457).
/// </summary>
public class ManejadorDeExcepciones : IExceptionHandler
{
    private readonly ILogger<ManejadorDeExcepciones> _log;

    public ManejadorDeExcepciones(ILogger<ManejadorDeExcepciones> log) => _log = log;

    public async ValueTask<bool> TryHandleAsync(HttpContext contexto, Exception ex, CancellationToken ct)
    {
        var (estado, titulo) = ex switch
        {
            NoEncontradoException => (StatusCodes.Status404NotFound, "Recurso no encontrado"),
            ReglaDeNegocioException => (StatusCodes.Status409Conflict, "Regla de negocio"),
            ValidacionException => (StatusCodes.Status400BadRequest, "Datos inválidos"),
            CredencialesInvalidasException => (StatusCodes.Status401Unauthorized, "Credenciales inválidas"),
            ServicioExternoNoDisponibleException => (StatusCodes.Status503ServiceUnavailable, "Servicio externo no disponible"),
            _ => (StatusCodes.Status500InternalServerError, "Error interno")
        };

        if (estado == StatusCodes.Status500InternalServerError)
            _log.LogError(ex, "Error no controlado en {Metodo} {Ruta}", contexto.Request.Method, contexto.Request.Path);
        else
            _log.LogWarning("{Titulo}: {Mensaje}", titulo, ex.Message);

        contexto.Response.StatusCode = estado;
        await contexto.Response.WriteAsJsonAsync(new ProblemDetails
        {
            Status = estado,
            Title = titulo,
            // Un error inesperado no expone su mensaje (puede traer rutas o datos internos); los de negocio sí.
            Detail = estado == StatusCodes.Status500InternalServerError
                ? "Ocurrió un error inesperado. Intentá de nuevo."
                : ex.Message
        }, ct);
        return true;
    }
}
