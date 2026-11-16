using AlquilerCanchas.Domain.Entities;
using AlquilerCanchas.Domain.Enums;
using AlquilerCanchas.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace AlquilerCanchas.Infrastructure.Persistence.Repositorios;

public class ClienteRepositorio : Repositorio<Cliente>, IClienteRepositorio
{
    public ClienteRepositorio(AppDbContext contexto) : base(contexto) { }

    public Task<bool> ExisteEmailAsync(string email, int? excluirId = null, CancellationToken ct = default) =>
        Conjunto.AnyAsync(c => c.Email == email && (excluirId == null || c.Id != excluirId), ct);

    public Task<bool> TieneReservasAsync(int clienteId, CancellationToken ct = default) =>
        Contexto.Reservas.AnyAsync(r => r.ClienteId == clienteId, ct);
}

public class CanchaRepositorio : Repositorio<Cancha>, ICanchaRepositorio
{
    public CanchaRepositorio(AppDbContext contexto) : base(contexto) { }

    public Task<bool> TieneReservasAsync(int canchaId, CancellationToken ct = default) =>
        Contexto.Reservas.AnyAsync(r => r.CanchaId == canchaId, ct);
}

public class ReservaRepositorio : Repositorio<Reserva>, IReservaRepositorio
{
    public ReservaRepositorio(AppDbContext contexto) : base(contexto) { }

    public Task<Reserva?> ObtenerDetalleAsync(int id, CancellationToken ct = default) =>
        Conjunto
            .Include(r => r.Cliente)
            .Include(r => r.Cancha)
            .Include(r => r.Pagos)
            .FirstOrDefaultAsync(r => r.Id == id, ct);

    public async Task<IReadOnlyList<Reserva>> ListarAsync(
        DateOnly? fecha, int? canchaId, int? clienteId, CancellationToken ct = default)
    {
        var consulta = Conjunto
            .AsNoTracking()
            .Include(r => r.Cliente)
            .Include(r => r.Cancha)
            .Include(r => r.Pagos)
            .AsQueryable();

        if (fecha is not null) consulta = consulta.Where(r => r.Fecha == fecha);
        if (canchaId is not null) consulta = consulta.Where(r => r.CanchaId == canchaId);
        if (clienteId is not null) consulta = consulta.Where(r => r.ClienteId == clienteId);

        return await consulta.OrderBy(r => r.Fecha).ThenBy(r => r.HoraInicio).ToListAsync(ct);
    }

    public Task<bool> HaySuperposicionAsync(
        int canchaId, DateOnly fecha, TimeOnly inicio, TimeOnly fin,
        int? excluirReservaId = null, CancellationToken ct = default) =>
        // Dos franjas [a,b) y [c,d) se pisan si a < d y c < b; así dos reservas contiguas no chocan.
        Conjunto.AnyAsync(r =>
            r.CanchaId == canchaId
            && r.Fecha == fecha
            && r.Estado != EstadoReserva.Cancelada
            && (excluirReservaId == null || r.Id != excluirReservaId)
            && r.HoraInicio < fin
            && inicio < r.HoraFin, ct);
}

public class PagoRepositorio : Repositorio<Pago>, IPagoRepositorio
{
    public PagoRepositorio(AppDbContext contexto) : base(contexto) { }

    public async Task<IReadOnlyList<Pago>> ListarPorReservaAsync(int reservaId, CancellationToken ct = default) =>
        await Conjunto.AsNoTracking().Where(p => p.ReservaId == reservaId).OrderBy(p => p.Id).ToListAsync(ct);

    public async Task<decimal> SumarMontosAsync(int reservaId, CancellationToken ct = default) =>
        await Conjunto.Where(p => p.ReservaId == reservaId).SumAsync(p => p.Monto, ct);
}

public class UsuarioRepositorio : Repositorio<Usuario>, IUsuarioRepositorio
{
    public UsuarioRepositorio(AppDbContext contexto) : base(contexto) { }

    public Task<Usuario?> ObtenerPorEmailAsync(string email, CancellationToken ct = default) =>
        Conjunto.FirstOrDefaultAsync(u => u.Email == email, ct);

    public Task<bool> ExistenUsuariosAsync(CancellationToken ct = default) => Conjunto.AnyAsync(ct);
}
