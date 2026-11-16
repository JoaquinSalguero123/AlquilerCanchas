using AlquilerCanchas.Application.Dtos;
using AlquilerCanchas.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AlquilerCanchas.WebAPI.Controllers;

[ApiController]
public class PagosController : ControllerBase
{
    private readonly IPagoServicio _pagos;

    public PagosController(IPagoServicio pagos) => _pagos = pagos;

    // Los pagos se consultan y crean "dentro" de una reserva porque no existen sin ella (diagrama: Reserva 1—N Pago).
    [HttpGet("api/reservas/{reservaId:int}/pagos")]
    public async Task<ActionResult<IReadOnlyList<PagoDto>>> ListarPorReserva(int reservaId, CancellationToken ct) =>
        Ok(await _pagos.ListarPorReservaAsync(reservaId, ct));

    [HttpPost("api/reservas/{reservaId:int}/pagos")]
    public async Task<ActionResult<PagoDto>> Registrar(int reservaId, PagoCrearDto dto, CancellationToken ct)
    {
        var pago = await _pagos.RegistrarAsync(reservaId, dto, ct);
        return CreatedAtAction(nameof(ListarPorReserva), new { reservaId }, pago);
    }

    // Anular un cobro cambia la contabilidad: queda reservado al administrador.
    [HttpDelete("api/pagos/{id:int}")]
    [Authorize(Roles = "Administrador")]
    public async Task<IActionResult> Eliminar(int id, CancellationToken ct)
    {
        await _pagos.EliminarAsync(id, ct);
        return NoContent();
    }
}
