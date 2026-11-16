using AlquilerCanchas.Application.Dtos;
using AlquilerCanchas.Application.Interfaces;
using AlquilerCanchas.Domain.Entities;
using AlquilerCanchas.Domain.Enums;
using AlquilerCanchas.Domain.Exceptions;
using AlquilerCanchas.Domain.Interfaces;

namespace AlquilerCanchas.Application.Services;

public class PagoServicio : IPagoServicio
{
    private readonly IPagoRepositorio _pagos;
    private readonly IReservaRepositorio _reservas;
    private readonly TimeProvider _reloj;

    public PagoServicio(IPagoRepositorio pagos, IReservaRepositorio reservas, TimeProvider reloj)
    {
        _pagos = pagos;
        _reservas = reservas;
        _reloj = reloj;
    }

    public async Task<IReadOnlyList<PagoDto>> ListarPorReservaAsync(int reservaId, CancellationToken ct = default)
    {
        await BuscarReservaAsync(reservaId, ct);
        return (await _pagos.ListarPorReservaAsync(reservaId, ct)).Select(Mapear).ToList();
    }

    public async Task<PagoDto> RegistrarAsync(int reservaId, PagoCrearDto dto, CancellationToken ct = default)
    {
        var reserva = await BuscarReservaAsync(reservaId, ct);

        if (reserva.Estado == EstadoReserva.Cancelada)
            throw new ReglaDeNegocioException("No se pueden registrar pagos sobre una reserva cancelada.");

        // Evita el sobrepago: seña + saldo nunca pueden superar el total de la reserva.
        var total = reserva.CalcularTotal(reserva.Cancha!.PrecioPorHora);
        var pagado = reserva.Pagos.Sum(p => p.Monto);
        var saldo = total - pagado;
        if (dto.Monto > saldo)
            throw new ReglaDeNegocioException($"El monto supera el saldo pendiente de la reserva ({saldo:0.00}).");

        var pago = new Pago
        {
            ReservaId = reservaId,
            Monto = dto.Monto,
            MetodoPago = dto.MetodoPago,
            Fecha = DateOnly.FromDateTime(_reloj.GetLocalNow().DateTime)
        };
        await _pagos.AgregarAsync(pago, ct);
        await _pagos.GuardarCambiosAsync(ct);
        return Mapear(pago);
    }

    public async Task EliminarAsync(int id, CancellationToken ct = default)
    {
        var pago = await _pagos.ObtenerPorIdAsync(id, ct) ?? throw new NoEncontradoException("un pago", id);
        _pagos.Eliminar(pago);
        await _pagos.GuardarCambiosAsync(ct);
    }

    private async Task<Reserva> BuscarReservaAsync(int reservaId, CancellationToken ct) =>
        await _reservas.ObtenerDetalleAsync(reservaId, ct) ?? throw new NoEncontradoException("una reserva", reservaId);

    private static PagoDto Mapear(Pago p) => new(p.Id, p.ReservaId, p.Monto, p.Fecha, p.MetodoPago);
}
