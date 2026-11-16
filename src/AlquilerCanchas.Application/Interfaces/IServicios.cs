using AlquilerCanchas.Application.Dtos;

namespace AlquilerCanchas.Application.Interfaces;

public interface IClienteServicio
{
    Task<IReadOnlyList<ClienteDto>> ListarAsync(CancellationToken ct = default);
    Task<ClienteDto> ObtenerAsync(int id, CancellationToken ct = default);
    Task<ClienteDto> CrearAsync(ClienteGuardarDto dto, CancellationToken ct = default);
    Task<ClienteDto> ActualizarAsync(int id, ClienteGuardarDto dto, CancellationToken ct = default);
    Task EliminarAsync(int id, CancellationToken ct = default);
}

public interface ICanchaServicio
{
    Task<IReadOnlyList<CanchaDto>> ListarAsync(CancellationToken ct = default);
    Task<CanchaDto> ObtenerAsync(int id, CancellationToken ct = default);
    Task<CanchaDto> CrearAsync(CanchaGuardarDto dto, CancellationToken ct = default);
    Task<CanchaDto> ActualizarAsync(int id, CanchaGuardarDto dto, CancellationToken ct = default);
    Task EliminarAsync(int id, CancellationToken ct = default);
}

public interface IReservaServicio
{
    Task<IReadOnlyList<ReservaDto>> ListarAsync(
        DateOnly? fecha, int? canchaId, int? clienteId, CancellationToken ct = default);
    Task<ReservaDto> ObtenerAsync(int id, CancellationToken ct = default);
    Task<ReservaDto> CrearAsync(ReservaGuardarDto dto, CancellationToken ct = default);
    Task<ReservaDto> ActualizarAsync(int id, ReservaGuardarDto dto, CancellationToken ct = default);
    Task<ReservaDto> ConfirmarAsync(int id, CancellationToken ct = default);
    Task<ReservaDto> CancelarAsync(int id, CancellationToken ct = default);

    /// <summary>Consulta un servicio externo de pronóstico para la franja de la reserva.</summary>
    Task<ClimaReservaDto> ObtenerClimaAsync(int id, CancellationToken ct = default);
}

public interface IPagoServicio
{
    Task<IReadOnlyList<PagoDto>> ListarPorReservaAsync(int reservaId, CancellationToken ct = default);
    Task<PagoDto> RegistrarAsync(int reservaId, PagoCrearDto dto, CancellationToken ct = default);
    Task EliminarAsync(int id, CancellationToken ct = default);
}

public interface IAuthServicio
{
    Task<TokenDto> LoginAsync(LoginDto dto, CancellationToken ct = default);
    Task RegistrarAsync(RegistrarUsuarioDto dto, CancellationToken ct = default);
}
