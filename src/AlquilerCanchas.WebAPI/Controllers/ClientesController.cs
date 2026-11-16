using AlquilerCanchas.Application.Dtos;
using AlquilerCanchas.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AlquilerCanchas.WebAPI.Controllers;

[ApiController]
[Route("api/clientes")]
public class ClientesController : ControllerBase
{
    private readonly IClienteServicio _clientes;

    public ClientesController(IClienteServicio clientes) => _clientes = clientes;

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ClienteDto>>> Listar(CancellationToken ct) =>
        Ok(await _clientes.ListarAsync(ct));

    [HttpGet("{id:int}")]
    public async Task<ActionResult<ClienteDto>> Obtener(int id, CancellationToken ct) =>
        Ok(await _clientes.ObtenerAsync(id, ct));

    [HttpPost]
    public async Task<ActionResult<ClienteDto>> Crear(ClienteGuardarDto dto, CancellationToken ct)
    {
        var creado = await _clientes.CrearAsync(dto, ct);
        return CreatedAtAction(nameof(Obtener), new { id = creado.Id }, creado);
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<ClienteDto>> Actualizar(int id, ClienteGuardarDto dto, CancellationToken ct) =>
        Ok(await _clientes.ActualizarAsync(id, dto, ct));

    [HttpDelete("{id:int}")]
    [Authorize(Roles = "Administrador")]
    public async Task<IActionResult> Eliminar(int id, CancellationToken ct)
    {
        await _clientes.EliminarAsync(id, ct);
        return NoContent();
    }
}
