namespace AlquilerCanchas.Domain.Entities;

/// <summary>
/// Espacio deportivo alquilable. El deporte es un atributo (no una subclase) para que el
/// mismo complejo ofrezca pádel, fútbol o tenis sin cambiar el modelo (ver minuta de relevamiento).
/// </summary>
public class Cancha : EntidadBase
{
    public string Nombre { get; set; } = string.Empty;
    public string TipoDeporte { get; set; } = string.Empty;
    public decimal PrecioPorHora { get; set; }

    public ICollection<Reserva> Reservas { get; set; } = new List<Reserva>();
}
