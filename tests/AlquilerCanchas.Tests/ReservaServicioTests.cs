using AlquilerCanchas.Application.Dtos;
using AlquilerCanchas.Application.Interfaces;
using AlquilerCanchas.Application.Services;
using AlquilerCanchas.Domain.Entities;
using AlquilerCanchas.Domain.Enums;
using AlquilerCanchas.Domain.Exceptions;
using AlquilerCanchas.Domain.Interfaces;
using NSubstitute;

namespace AlquilerCanchas.Tests;

/// <summary>Reloj con hora fija: los tests de fechas no pueden depender del día en que se ejecutan.</summary>
internal sealed class RelojFijo(DateTime ahora) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => new(DateTime.SpecifyKind(ahora, DateTimeKind.Utc));
    public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
}

public class ReservaServicioTests
{
    private static readonly DateOnly Hoy = new(2026, 12, 1);
    private static readonly DateOnly Manana = Hoy.AddDays(1);

    private readonly IReservaRepositorio _reservas = Substitute.For<IReservaRepositorio>();
    private readonly IClienteRepositorio _clientes = Substitute.For<IClienteRepositorio>();
    private readonly ICanchaRepositorio _canchas = Substitute.For<ICanchaRepositorio>();
    private readonly IProveedorDeClima _clima = Substitute.For<IProveedorDeClima>();
    private readonly ReservaServicio _servicio;

    public ReservaServicioTests()
    {
        _clientes.ObtenerPorIdAsync(1, Arg.Any<CancellationToken>()).Returns(new Cliente { Id = 1, Nombre = "Ana", Apellido = "Pérez" });
        _canchas.ObtenerPorIdAsync(1, Arg.Any<CancellationToken>()).Returns(new Cancha { Id = 1, Nombre = "Pádel 1", PrecioPorHora = 20000m });
        _servicio = new ReservaServicio(_reservas, _clientes, _canchas, _clima, new RelojFijo(Hoy.ToDateTime(new TimeOnly(10, 0))));
    }

    private static ReservaGuardarDto Dto(string inicio = "18:00", string fin = "19:30", DateOnly? fecha = null, int cliente = 1, int cancha = 1) =>
        new(cliente, cancha, fecha ?? Manana, TimeOnly.Parse(inicio), TimeOnly.Parse(fin));

    private static Reserva ReservaGuardada(EstadoReserva estado, params Pago[] pagos) => new()
    {
        Id = 7, Fecha = Manana, HoraInicio = new(18, 0), HoraFin = new(19, 30), Estado = estado,
        ClienteId = 1, CanchaId = 1,
        Cliente = new Cliente { Id = 1, Nombre = "Ana", Apellido = "Pérez" },
        Cancha = new Cancha { Id = 1, Nombre = "Pádel 1", PrecioPorHora = 20000m },
        Pagos = pagos
    };

    [Fact]
    public async Task Crear_con_fin_anterior_al_inicio_se_rechaza()
    {
        await Assert.ThrowsAsync<ValidacionException>(() => _servicio.CrearAsync(Dto("19:00", "18:00")));
        await _reservas.DidNotReceive().AgregarAsync(Arg.Any<Reserva>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Crear_en_fecha_pasada_se_rechaza()
    {
        await Assert.ThrowsAsync<ValidacionException>(() => _servicio.CrearAsync(Dto(fecha: Hoy.AddDays(-1))));
    }

    [Fact]
    public async Task Crear_con_cliente_inexistente_devuelve_no_encontrado()
    {
        await Assert.ThrowsAsync<NoEncontradoException>(() => _servicio.CrearAsync(Dto(cliente: 99)));
    }

    [Fact]
    public async Task Crear_en_franja_ocupada_se_rechaza_y_no_guarda()
    {
        _reservas.HaySuperposicionAsync(1, Manana, new(18, 0), new(19, 30), null, Arg.Any<CancellationToken>()).Returns(true);

        await Assert.ThrowsAsync<ReglaDeNegocioException>(() => _servicio.CrearAsync(Dto()));
        await _reservas.DidNotReceive().GuardarCambiosAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Crear_calcula_el_total_por_las_horas_reservadas_y_nace_pendiente()
    {
        _reservas.ObtenerDetalleAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(_ => ReservaGuardada(EstadoReserva.Pendiente));

        var resultado = await _servicio.CrearAsync(Dto());

        Assert.Equal(EstadoReserva.Pendiente, resultado.Estado);
        Assert.Equal(30000m, resultado.Total); // 1,5 h x 20000
        Assert.Equal(30000m, resultado.Saldo);
        await _reservas.Received(1).AgregarAsync(
            Arg.Is<Reserva>(r => r.Estado == EstadoReserva.Pendiente), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Modificar_una_reserva_confirmada_se_rechaza()
    {
        _reservas.ObtenerDetalleAsync(7, Arg.Any<CancellationToken>()).Returns(ReservaGuardada(EstadoReserva.Confirmada));
        await Assert.ThrowsAsync<ReglaDeNegocioException>(() => _servicio.ActualizarAsync(7, Dto()));
    }

    [Fact]
    public async Task Modificar_excluye_la_propia_reserva_al_buscar_superposiciones()
    {
        _reservas.ObtenerDetalleAsync(7, Arg.Any<CancellationToken>()).Returns(ReservaGuardada(EstadoReserva.Pendiente));

        await _servicio.ActualizarAsync(7, Dto("18:30", "19:30"));

        // Sin este filtro, mover una reserva 30 minutos chocaría con ella misma.
        await _reservas.Received(1).HaySuperposicionAsync(
            1, Manana, new(18, 30), new(19, 30), 7, Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(EstadoReserva.Confirmada)]
    [InlineData(EstadoReserva.Cancelada)]
    public async Task Confirmar_sólo_está_permitido_desde_pendiente(EstadoReserva estadoActual)
    {
        _reservas.ObtenerDetalleAsync(7, Arg.Any<CancellationToken>()).Returns(ReservaGuardada(estadoActual));
        await Assert.ThrowsAsync<ReglaDeNegocioException>(() => _servicio.ConfirmarAsync(7));
    }

    [Fact]
    public async Task Confirmar_una_pendiente_la_deja_confirmada()
    {
        _reservas.ObtenerDetalleAsync(7, Arg.Any<CancellationToken>()).Returns(ReservaGuardada(EstadoReserva.Pendiente));
        var r = await _servicio.ConfirmarAsync(7);
        Assert.Equal(EstadoReserva.Confirmada, r.Estado);
    }

    [Fact]
    public async Task Cancelar_dos_veces_se_rechaza()
    {
        _reservas.ObtenerDetalleAsync(7, Arg.Any<CancellationToken>()).Returns(ReservaGuardada(EstadoReserva.Cancelada));
        await Assert.ThrowsAsync<ReglaDeNegocioException>(() => _servicio.CancelarAsync(7));
    }

    [Fact]
    public async Task Clima_sólo_considera_las_horas_de_la_franja_y_recomienda_suspender_con_lluvia_alta()
    {
        _reservas.ObtenerDetalleAsync(7, Arg.Any<CancellationToken>()).Returns(ReservaGuardada(EstadoReserva.Pendiente));
        DateTime H(int hora) => Manana.ToDateTime(new TimeOnly(hora, 0));
        _clima.ObtenerPronosticoDelDiaAsync(Manana, Arg.Any<CancellationToken>()).Returns(new List<PronosticoHorario>
        {
            new(H(17), 10m, 0),    // fuera: antes de las 18
            new(H(18), 20m, 30),   // dentro
            new(H(19), 22m, 70),   // dentro (la reserva llega hasta 19:30)
            new(H(20), 40m, 100)   // fuera: después del fin
        });

        var clima = await _servicio.ObtenerClimaAsync(7);

        Assert.Equal(21m, clima.TemperaturaPromedioC);
        Assert.Equal(70, clima.ProbabilidadMaxLluviaPorcentaje);
        Assert.True(clima.RecomendacionSuspender);
    }

    [Fact]
    public async Task Clima_sin_pronostico_disponible_informa_una_regla_de_negocio()
    {
        _reservas.ObtenerDetalleAsync(7, Arg.Any<CancellationToken>()).Returns(ReservaGuardada(EstadoReserva.Pendiente));
        _clima.ObtenerPronosticoDelDiaAsync(Manana, Arg.Any<CancellationToken>()).Returns(new List<PronosticoHorario>());

        await Assert.ThrowsAsync<ReglaDeNegocioException>(() => _servicio.ObtenerClimaAsync(7));
    }
}
