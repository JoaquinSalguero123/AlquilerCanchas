using AlquilerCanchas.Application.Dtos;
using AlquilerCanchas.Application.Interfaces;
using AlquilerCanchas.Domain.Entities;
using AlquilerCanchas.Domain.Enums;
using AlquilerCanchas.Domain.Exceptions;
using AlquilerCanchas.Domain.Interfaces;

namespace AlquilerCanchas.Application.Services;

public class ReservaServicio : IReservaServicio
{
    /// <summary>Umbral a partir del cual se recomienda suspender; criterio de negocio, no del proveedor de clima.</summary>
    private const int LluviaQueRecomiendaSuspender = 60;

    private readonly IReservaRepositorio _reservas;
    private readonly IClienteRepositorio _clientes;
    private readonly ICanchaRepositorio _canchas;
    private readonly IProveedorDeClima _clima;
    private readonly TimeProvider _reloj;

    public ReservaServicio(
        IReservaRepositorio reservas,
        IClienteRepositorio clientes,
        ICanchaRepositorio canchas,
        IProveedorDeClima clima,
        TimeProvider reloj)
    {
        _reservas = reservas;
        _clientes = clientes;
        _canchas = canchas;
        _clima = clima;
        _reloj = reloj;
    }

    public async Task<IReadOnlyList<ReservaDto>> ListarAsync(
        DateOnly? fecha, int? canchaId, int? clienteId, CancellationToken ct = default) =>
        (await _reservas.ListarAsync(fecha, canchaId, clienteId, ct)).Select(Mapear).ToList();

    public async Task<ReservaDto> ObtenerAsync(int id, CancellationToken ct = default) =>
        Mapear(await BuscarDetalleAsync(id, ct));

    public async Task<ReservaDto> CrearAsync(ReservaGuardarDto dto, CancellationToken ct = default)
    {
        ValidarFranja(dto);
        await ValidarReferenciasAsync(dto, ct);
        await ValidarDisponibilidadAsync(dto, null, ct);

        var reserva = new Reserva { Estado = EstadoReserva.Pendiente };
        Aplicar(reserva, dto);
        await _reservas.AgregarAsync(reserva, ct);
        await _reservas.GuardarCambiosAsync(ct);

        // Se recarga para devolver Cliente, Cancha y totales ya resueltos.
        return Mapear(await BuscarDetalleAsync(reserva.Id, ct));
    }

    public async Task<ReservaDto> ActualizarAsync(int id, ReservaGuardarDto dto, CancellationToken ct = default)
    {
        var reserva = await BuscarDetalleAsync(id, ct);

        // Una vez confirmada o cancelada la reserva es un compromiso con el cliente: para cambiarla
        // hay que cancelarla y crear otra, que además vuelve a validar disponibilidad.
        if (reserva.Estado != EstadoReserva.Pendiente)
            throw new ReglaDeNegocioException($"Sólo se pueden modificar reservas pendientes (está {reserva.Estado}).");

        ValidarFranja(dto);
        await ValidarReferenciasAsync(dto, ct);
        await ValidarDisponibilidadAsync(dto, id, ct);

        Aplicar(reserva, dto);
        _reservas.Actualizar(reserva);
        await _reservas.GuardarCambiosAsync(ct);
        return Mapear(await BuscarDetalleAsync(id, ct));
    }

    public async Task<ReservaDto> ConfirmarAsync(int id, CancellationToken ct = default)
    {
        var reserva = await BuscarDetalleAsync(id, ct);

        if (reserva.Estado != EstadoReserva.Pendiente)
            throw new ReglaDeNegocioException($"Sólo se puede confirmar una reserva pendiente (está {reserva.Estado}).");

        reserva.Estado = EstadoReserva.Confirmada;
        _reservas.Actualizar(reserva);
        await _reservas.GuardarCambiosAsync(ct);
        return Mapear(reserva);
    }

    public async Task<ReservaDto> CancelarAsync(int id, CancellationToken ct = default)
    {
        var reserva = await BuscarDetalleAsync(id, ct);

        if (reserva.Estado == EstadoReserva.Cancelada)
            throw new ReglaDeNegocioException("La reserva ya está cancelada.");

        // Cancelar libera la franja (las canceladas no cuentan para la superposición).
        // Los pagos ya cobrados se conservan: la devolución es un trámite del complejo, fuera del modelo aprobado.
        reserva.Estado = EstadoReserva.Cancelada;
        _reservas.Actualizar(reserva);
        await _reservas.GuardarCambiosAsync(ct);
        return Mapear(reserva);
    }

    public async Task<ClimaReservaDto> ObtenerClimaAsync(int id, CancellationToken ct = default)
    {
        var reserva = await BuscarDetalleAsync(id, ct);

        var horas = await _clima.ObtenerPronosticoDelDiaAsync(reserva.Fecha, ct);

        // La reserva ocupa [inicio, fin): una de 18:00 a 19:00 sólo mira la hora de las 18.
        var primeraHora = reserva.HoraInicio.Hour;
        var ultimaHora = reserva.HoraFin.Minute == 0 ? reserva.HoraFin.Hour - 1 : reserva.HoraFin.Hour;
        var enFranja = horas
            .Where(h => h.HoraLocal.Hour >= primeraHora && h.HoraLocal.Hour <= ultimaHora)
            .ToList();

        if (enFranja.Count == 0)
            throw new ReglaDeNegocioException(
                "No hay pronóstico disponible para esa fecha (el proveedor sólo cubre los próximos días).");

        var lluviaMaxima = enFranja.Max(h => h.ProbabilidadLluviaPorcentaje);
        return new ClimaReservaDto(
            reserva.Id,
            reserva.Fecha,
            reserva.HoraInicio,
            reserva.HoraFin,
            Math.Round(enFranja.Average(h => h.TemperaturaC), 1),
            lluviaMaxima,
            lluviaMaxima >= LluviaQueRecomiendaSuspender,
            "Open-Meteo");
    }

    private static void ValidarFranja(ReservaGuardarDto dto)
    {
        if (dto.HoraFin <= dto.HoraInicio)
            throw new ValidacionException("La hora de fin debe ser posterior a la de inicio.");
    }

    private async Task ValidarReferenciasAsync(ReservaGuardarDto dto, CancellationToken ct)
    {
        var hoy = DateOnly.FromDateTime(_reloj.GetLocalNow().DateTime);
        if (dto.Fecha < hoy)
            throw new ValidacionException("No se puede reservar en una fecha pasada.");

        if (await _clientes.ObtenerPorIdAsync(dto.ClienteId, ct) is null)
            throw new NoEncontradoException("un cliente", dto.ClienteId);
        if (await _canchas.ObtenerPorIdAsync(dto.CanchaId, ct) is null)
            throw new NoEncontradoException("una cancha", dto.CanchaId);
    }

    private async Task ValidarDisponibilidadAsync(ReservaGuardarDto dto, int? excluirReservaId, CancellationToken ct)
    {
        if (await _reservas.HaySuperposicionAsync(
                dto.CanchaId, dto.Fecha, dto.HoraInicio, dto.HoraFin, excluirReservaId, ct))
            throw new ReglaDeNegocioException("La cancha ya está reservada en esa franja horaria.");
    }

    private async Task<Reserva> BuscarDetalleAsync(int id, CancellationToken ct) =>
        await _reservas.ObtenerDetalleAsync(id, ct) ?? throw new NoEncontradoException("una reserva", id);

    private static void Aplicar(Reserva r, ReservaGuardarDto d)
    {
        r.ClienteId = d.ClienteId;
        r.CanchaId = d.CanchaId;
        r.Fecha = d.Fecha;
        r.HoraInicio = d.HoraInicio;
        r.HoraFin = d.HoraFin;
    }

    private static ReservaDto Mapear(Reserva r)
    {
        var total = r.CalcularTotal(r.Cancha!.PrecioPorHora);
        var pagado = r.Pagos.Sum(p => p.Monto);
        return new ReservaDto(
            r.Id, r.Fecha, r.HoraInicio, r.HoraFin, r.Estado,
            r.ClienteId, $"{r.Cliente!.Nombre} {r.Cliente.Apellido}",
            r.CanchaId, r.Cancha.Nombre,
            total, pagado, total - pagado);
    }
}
