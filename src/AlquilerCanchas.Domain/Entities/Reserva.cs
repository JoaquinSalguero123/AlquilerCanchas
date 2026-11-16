using AlquilerCanchas.Domain.Enums;

namespace AlquilerCanchas.Domain.Entities;

/// <summary>Alquiler de una cancha en una fecha y franja horaria; conecta Cliente con Cancha.</summary>
public class Reserva : EntidadBase
{
    public DateOnly Fecha { get; set; }
    public TimeOnly HoraInicio { get; set; }
    public TimeOnly HoraFin { get; set; }
    public EstadoReserva Estado { get; set; } = EstadoReserva.Pendiente;

    public int ClienteId { get; set; }
    public Cliente? Cliente { get; set; }

    public int CanchaId { get; set; }
    public Cancha? Cancha { get; set; }

    public ICollection<Pago> Pagos { get; set; } = new List<Pago>();

    /// <summary>
    /// El precio no se guarda: se deriva de la tarifa vigente de la cancha. Así el diagrama
    /// aprobado no se altera, a costa de que un cambio de tarifa afecte reservas futuras.
    /// </summary>
    public decimal CalcularTotal(decimal precioPorHora) =>
        Math.Round((decimal)(HoraFin - HoraInicio).TotalHours * precioPorHora, 2);
}
