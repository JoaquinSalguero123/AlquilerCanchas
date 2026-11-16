namespace AlquilerCanchas.Domain.Entities;

/// <summary>Persona que alquila canchas (diagrama de clases aprobado: Cliente).</summary>
public class Cliente : EntidadBase
{
    public string Nombre { get; set; } = string.Empty;
    public string Apellido { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Telefono { get; set; } = string.Empty;

    public ICollection<Reserva> Reservas { get; set; } = new List<Reserva>();
}
