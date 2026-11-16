using AlquilerCanchas.Domain.Enums;

namespace AlquilerCanchas.Domain.Entities;

/// <summary>
/// Cada cobro sobre una reserva. Es entidad aparte porque una reserva puede pagarse en
/// varios momentos (seña y saldo) y se quiere conservar el historial.
/// </summary>
public class Pago : EntidadBase
{
    public decimal Monto { get; set; }
    public DateOnly Fecha { get; set; }
    public MetodoPago MetodoPago { get; set; }

    public int ReservaId { get; set; }
    public Reserva? Reserva { get; set; }
}
