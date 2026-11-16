using System.ComponentModel.DataAnnotations;
using AlquilerCanchas.Domain.Enums;

namespace AlquilerCanchas.Application.Dtos;

public record PagoDto(int Id, int ReservaId, decimal Monto, DateOnly Fecha, MetodoPago MetodoPago);

/// <summary>La fecha la pone el servidor (es la del cobro real); el cliente no puede falsearla.</summary>
public record PagoCrearDto(
    [Range(0.01, 100_000_000)] decimal Monto,
    MetodoPago MetodoPago);
