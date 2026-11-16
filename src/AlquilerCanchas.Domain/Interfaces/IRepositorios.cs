using AlquilerCanchas.Domain.Entities;

namespace AlquilerCanchas.Domain.Interfaces;

/// <summary>
/// Repositorio genérico (CRUD común). Cada repositorio específico lo extiende sólo con las
/// consultas propias de su entidad, así el CRUD se escribe una única vez.
/// </summary>
public interface IRepositorio<T> where T : EntidadBase
{
    Task<IReadOnlyList<T>> ObtenerTodosAsync(CancellationToken ct = default);
    Task<T?> ObtenerPorIdAsync(int id, CancellationToken ct = default);
    Task AgregarAsync(T entidad, CancellationToken ct = default);
    void Actualizar(T entidad);
    void Eliminar(T entidad);

    /// <summary>Confirma los cambios pendientes. Un único SaveChanges por operación de negocio.</summary>
    Task<int> GuardarCambiosAsync(CancellationToken ct = default);
}

public interface IClienteRepositorio : IRepositorio<Cliente>
{
    Task<bool> ExisteEmailAsync(string email, int? excluirId = null, CancellationToken ct = default);
    Task<bool> TieneReservasAsync(int clienteId, CancellationToken ct = default);
}

public interface ICanchaRepositorio : IRepositorio<Cancha>
{
    Task<bool> TieneReservasAsync(int canchaId, CancellationToken ct = default);
}

public interface IReservaRepositorio : IRepositorio<Reserva>
{
    /// <summary>Reserva con Cliente, Cancha y Pagos cargados.</summary>
    Task<Reserva?> ObtenerDetalleAsync(int id, CancellationToken ct = default);

    Task<IReadOnlyList<Reserva>> ListarAsync(
        DateOnly? fecha, int? canchaId, int? clienteId, CancellationToken ct = default);

    /// <summary>
    /// Hay otra reserva NO cancelada de la misma cancha, el mismo día, cuya franja se pisa
    /// con [inicio, fin). Los extremos que sólo se tocan (una termina 18:00, otra empieza 18:00) no cuentan.
    /// </summary>
    Task<bool> HaySuperposicionAsync(
        int canchaId, DateOnly fecha, TimeOnly inicio, TimeOnly fin,
        int? excluirReservaId = null, CancellationToken ct = default);
}

public interface IPagoRepositorio : IRepositorio<Pago>
{
    Task<IReadOnlyList<Pago>> ListarPorReservaAsync(int reservaId, CancellationToken ct = default);
    Task<decimal> SumarMontosAsync(int reservaId, CancellationToken ct = default);
}

public interface IUsuarioRepositorio : IRepositorio<Usuario>
{
    Task<Usuario?> ObtenerPorEmailAsync(string email, CancellationToken ct = default);
    Task<bool> ExistenUsuariosAsync(CancellationToken ct = default);
}
