using AlquilerCanchas.Application.Dtos;
using AlquilerCanchas.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AlquilerCanchas.WebAPI.Controllers;

[ApiController]
[Route("api/canchas")]
public class CanchasController : ControllerBase
{
    private readonly ICanchaServicio _canchas;

    public CanchasController(ICanchaServicio canchas) => _canchas = canchas;

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<CanchaDto>>> Listar(CancellationToken ct) =>
        Ok(await _canchas.ListarAsync(ct));

    [HttpGet("{id:int}")]
    public async Task<ActionResult<CanchaDto>> Obtener(int id, CancellationToken ct) =>
        Ok(await _canchas.ObtenerAsync(id, ct));

    // Cambiar tarifas o dar de alta/baja canchas es una decisión del dueño, no del personal de mostrador.
    [HttpPost]
    [Authorize(Roles = "Administrador")]
    public async Task<ActionResult<CanchaDto>> Crear(CanchaGuardarDto dto, CancellationToken ct)
    {
        var creada = await _canchas.CrearAsync(dto, ct);
        return CreatedAtAction(nameof(Obtener), new { id = creada.Id }, creada);
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = "Administrador")]
    public async Task<ActionResult<CanchaDto>> Actualizar(int id, CanchaGuardarDto dto, CancellationToken ct) =>
        Ok(await _canchas.ActualizarAsync(id, dto, ct));

    [HttpDelete("{id:int}")]
    [Authorize(Roles = "Administrador")]
    public async Task<IActionResult> Eliminar(int id, CancellationToken ct)
    {
        await _canchas.EliminarAsync(id, ct);
        return NoContent();
    }
}
