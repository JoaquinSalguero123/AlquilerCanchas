using AlquilerCanchas.Application.Dtos;
using AlquilerCanchas.Application.Services;
using AlquilerCanchas.Domain.Entities;
using AlquilerCanchas.Domain.Enums;
using AlquilerCanchas.Domain.Exceptions;
using AlquilerCanchas.Domain.Interfaces;
using NSubstitute;

namespace AlquilerCanchas.Tests;

public class PagoServicioTests
{
    private static readonly DateOnly Hoy = new(2026, 12, 1);

    private readonly IPagoRepositorio _pagos = Substitute.For<IPagoRepositorio>();
    private readonly IReservaRepositorio _reservas = Substitute.For<IReservaRepositorio>();
    private readonly PagoServicio _servicio;

    public PagoServicioTests() =>
        _servicio = new PagoServicio(_pagos, _reservas, new RelojFijo(Hoy.ToDateTime(new TimeOnly(23, 30))));

    /// <summary>Reserva de 2 h a 10.000 la hora: total 20.000.</summary>
    private void DadaUnaReserva(EstadoReserva estado, params decimal[] pagosPrevios) =>
        _reservas.ObtenerDetalleAsync(5, Arg.Any<CancellationToken>()).Returns(new Reserva
        {
            Id = 5, Estado = estado, HoraInicio = new(10, 0), HoraFin = new(12, 0),
            Cancha = new Cancha { PrecioPorHora = 10000m },
            Pagos = pagosPrevios.Select(m => new Pago { Monto = m }).ToList()
        });

    [Fact]
    public async Task Registrar_un_pago_dentro_del_saldo_lo_guarda_con_la_fecha_del_servidor()
    {
        DadaUnaReserva(EstadoReserva.Confirmada, 5000m);

        var pago = await _servicio.RegistrarAsync(5, new PagoCrearDto(15000m, MetodoPago.Transferencia));

        Assert.Equal(Hoy, pago.Fecha);
        await _pagos.Received(1).GuardarCambiosAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Registrar_un_pago_que_supera_el_saldo_se_rechaza()
    {
        DadaUnaReserva(EstadoReserva.Confirmada, 5000m); // saldo 15.000

        var ex = await Assert.ThrowsAsync<ReglaDeNegocioException>(
            () => _servicio.RegistrarAsync(5, new PagoCrearDto(15000.01m, MetodoPago.Efectivo)));

        Assert.Contains("saldo", ex.Message);
        await _pagos.DidNotReceive().AgregarAsync(Arg.Any<Pago>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Registrar_el_saldo_exacto_esta_permitido()
    {
        DadaUnaReserva(EstadoReserva.Pendiente, 5000m);
        await _servicio.RegistrarAsync(5, new PagoCrearDto(15000m, MetodoPago.Efectivo));
    }

    [Fact]
    public async Task Registrar_sobre_una_reserva_cancelada_se_rechaza()
    {
        DadaUnaReserva(EstadoReserva.Cancelada);
        await Assert.ThrowsAsync<ReglaDeNegocioException>(
            () => _servicio.RegistrarAsync(5, new PagoCrearDto(100m, MetodoPago.Efectivo)));
    }

    [Fact]
    public async Task Registrar_sobre_una_reserva_inexistente_devuelve_no_encontrado()
    {
        await Assert.ThrowsAsync<NoEncontradoException>(
            () => _servicio.RegistrarAsync(99, new PagoCrearDto(100m, MetodoPago.Efectivo)));
    }
}
