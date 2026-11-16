using AlquilerCanchas.Application.Dtos;
using AlquilerCanchas.Application.Interfaces;
using AlquilerCanchas.Domain.Entities;
using AlquilerCanchas.Domain.Exceptions;
using AlquilerCanchas.Domain.Interfaces;

namespace AlquilerCanchas.Application.Services;

public class CanchaServicio : ICanchaServicio
{
    private readonly ICanchaRepositorio _canchas;

    public CanchaServicio(ICanchaRepositorio canchas) => _canchas = canchas;

    public async Task<IReadOnlyList<CanchaDto>> ListarAsync(CancellationToken ct = default) =>
        (await _canchas.ObtenerTodosAsync(ct)).Select(Mapear).ToList();

    public async Task<CanchaDto> ObtenerAsync(int id, CancellationToken ct = default) =>
        Mapear(await BuscarAsync(id, ct));

    public async Task<CanchaDto> CrearAsync(CanchaGuardarDto dto, CancellationToken ct = default)
    {
        var cancha = new Cancha();
        Aplicar(cancha, dto);
        await _canchas.AgregarAsync(cancha, ct);
        await _canchas.GuardarCambiosAsync(ct);
        return Mapear(cancha);
    }

    public async Task<CanchaDto> ActualizarAsync(int id, CanchaGuardarDto dto, CancellationToken ct = default)
    {
        var cancha = await BuscarAsync(id, ct);
        Aplicar(cancha, dto);
        _canchas.Actualizar(cancha);
        await _canchas.GuardarCambiosAsync(ct);
        return Mapear(cancha);
    }

    public async Task EliminarAsync(int id, CancellationToken ct = default)
    {
        var cancha = await BuscarAsync(id, ct);

        if (await _canchas.TieneReservasAsync(id, ct))
            throw new ReglaDeNegocioException("No se puede eliminar una cancha que tiene reservas registradas.");

        _canchas.Eliminar(cancha);
        await _canchas.GuardarCambiosAsync(ct);
    }

    private async Task<Cancha> BuscarAsync(int id, CancellationToken ct) =>
        await _canchas.ObtenerPorIdAsync(id, ct) ?? throw new NoEncontradoException("una cancha", id);

    private static void Aplicar(Cancha c, CanchaGuardarDto d)
    {
        c.Nombre = d.Nombre.Trim();
        c.TipoDeporte = d.TipoDeporte.Trim();
        c.PrecioPorHora = d.PrecioPorHora;
    }

    private static CanchaDto Mapear(Cancha c) => new(c.Id, c.Nombre, c.TipoDeporte, c.PrecioPorHora);
}
