using AlquilerCanchas.Application.Dtos;
using AlquilerCanchas.Application.Interfaces;
using AlquilerCanchas.Domain.Entities;
using AlquilerCanchas.Domain.Exceptions;
using AlquilerCanchas.Domain.Interfaces;

namespace AlquilerCanchas.Application.Services;

public class ClienteServicio : IClienteServicio
{
    private readonly IClienteRepositorio _clientes;

    public ClienteServicio(IClienteRepositorio clientes) => _clientes = clientes;

    public async Task<IReadOnlyList<ClienteDto>> ListarAsync(CancellationToken ct = default) =>
        (await _clientes.ObtenerTodosAsync(ct)).Select(Mapear).ToList();

    public async Task<ClienteDto> ObtenerAsync(int id, CancellationToken ct = default) =>
        Mapear(await BuscarAsync(id, ct));

    public async Task<ClienteDto> CrearAsync(ClienteGuardarDto dto, CancellationToken ct = default)
    {
        await ValidarEmailLibreAsync(dto.Email, null, ct);

        var cliente = new Cliente();
        Aplicar(cliente, dto);
        await _clientes.AgregarAsync(cliente, ct);
        await _clientes.GuardarCambiosAsync(ct);
        return Mapear(cliente);
    }

    public async Task<ClienteDto> ActualizarAsync(int id, ClienteGuardarDto dto, CancellationToken ct = default)
    {
        var cliente = await BuscarAsync(id, ct);
        await ValidarEmailLibreAsync(dto.Email, id, ct);

        Aplicar(cliente, dto);
        _clientes.Actualizar(cliente);
        await _clientes.GuardarCambiosAsync(ct);
        return Mapear(cliente);
    }

    public async Task EliminarAsync(int id, CancellationToken ct = default)
    {
        var cliente = await BuscarAsync(id, ct);

        // Se bloquea en vez de borrar en cascada: perder reservas y pagos destruiría el historial de cobros.
        if (await _clientes.TieneReservasAsync(id, ct))
            throw new ReglaDeNegocioException("No se puede eliminar un cliente que tiene reservas registradas.");

        _clientes.Eliminar(cliente);
        await _clientes.GuardarCambiosAsync(ct);
    }

    private async Task<Cliente> BuscarAsync(int id, CancellationToken ct) =>
        await _clientes.ObtenerPorIdAsync(id, ct) ?? throw new NoEncontradoException("un cliente", id);

    private async Task ValidarEmailLibreAsync(string email, int? excluirId, CancellationToken ct)
    {
        if (await _clientes.ExisteEmailAsync(email.Trim(), excluirId, ct))
            throw new ReglaDeNegocioException($"Ya existe un cliente con el email {email}.");
    }

    private static void Aplicar(Cliente c, ClienteGuardarDto d)
    {
        c.Nombre = d.Nombre.Trim();
        c.Apellido = d.Apellido.Trim();
        c.Email = d.Email.Trim();
        c.Telefono = d.Telefono.Trim();
    }

    private static ClienteDto Mapear(Cliente c) => new(c.Id, c.Nombre, c.Apellido, c.Email, c.Telefono);
}
