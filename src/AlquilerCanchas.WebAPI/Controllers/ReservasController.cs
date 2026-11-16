using AlquilerCanchas.Application.Dtos;
using AlquilerCanchas.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace AlquilerCanchas.WebAPI.Controllers;

[ApiController]
[Route("api/reservas")]
public class ReservasController : ControllerBase
{
    private readonly IReservaServicio _reservas;

    public ReservasController(IReservaServicio reservas) => _reservas = reservas;

    /// <summary>Lista reservas; todos los filtros son opcionales (ej. la agenda de un día: ?fecha=2026-12-01).</summary>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ReservaDto>>> Listar(
        [FromQuery] DateOnly? fecha, [FromQuery] int? canchaId, [FromQuery] int? clienteId, CancellationToken ct) =>
        Ok(await _reservas.ListarAsync(fecha, canchaId, clienteId, ct));

    [HttpGet("{id:int}")]
    public async Task<ActionResult<ReservaDto>> Obtener(int id, CancellationToken ct) =>
        Ok(await _reservas.ObtenerAsync(id, ct));

    [HttpPost]
    public async Task<ActionResult<ReservaDto>> Crear(ReservaGuardarDto dto, CancellationToken ct)
    {
        var creada = await _reservas.CrearAsync(dto, ct);
        return CreatedAtAction(nameof(Obtener), new { id = creada.Id }, creada);
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<ReservaDto>> Actualizar(int id, ReservaGuardarDto dto, CancellationToken ct) =>
        Ok(await _reservas.ActualizarAsync(id, dto, ct));

    // Las transiciones de estado son acciones (POST) y no un PUT del campo "estado": así sólo
    // existen los cambios que el negocio permite (Pendiente→Confirmada, cualquiera→Cancelada).
    [HttpPost("{id:int}/confirmar")]
    public async Task<ActionResult<ReservaDto>> Confirmar(int id, CancellationToken ct) =>
        Ok(await _reservas.ConfirmarAsync(id, ct));

    [HttpPost("{id:int}/cancelar")]
    public async Task<ActionResult<ReservaDto>> Cancelar(int id, CancellationToken ct) =>
        Ok(await _reservas.CancelarAsync(id, ct));

    /// <summary>Pronóstico del clima para la franja de la reserva (consume Open-Meteo vía HttpClientFactory).</summary>
    [HttpGet("{id:int}/clima")]
    public async Task<ActionResult<ClimaReservaDto>> Clima(int id, CancellationToken ct) =>
        Ok(await _reservas.ObtenerClimaAsync(id, ct));
}
