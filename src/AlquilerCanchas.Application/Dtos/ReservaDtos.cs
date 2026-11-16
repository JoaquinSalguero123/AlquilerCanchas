using System.ComponentModel.DataAnnotations;
using AlquilerCanchas.Domain.Enums;

namespace AlquilerCanchas.Application.Dtos;

public record ReservaDto(
    int Id,
    DateOnly Fecha,
    TimeOnly HoraInicio,
    TimeOnly HoraFin,
    EstadoReserva Estado,
    int ClienteId,
    string Cliente,
    int CanchaId,
    string Cancha,
    decimal Total,
    decimal TotalPagado,
    decimal Saldo);

public record ReservaGuardarDto(
    [Range(1, int.MaxValue)] int ClienteId,
    [Range(1, int.MaxValue)] int CanchaId,
    DateOnly Fecha,
    TimeOnly HoraInicio,
    TimeOnly HoraFin);

/// <summary>Pronóstico del clima para la franja de una reserva (dato de un servicio de terceros).</summary>
public record ClimaReservaDto(
    int ReservaId,
    DateOnly Fecha,
    TimeOnly HoraInicio,
    TimeOnly HoraFin,
    decimal TemperaturaPromedioC,
    int ProbabilidadMaxLluviaPorcentaje,
    bool RecomendacionSuspender,
    string Fuente);
