namespace AlquilerCanchas.Domain.Entities;

/// <summary>
/// Base común de todas las entidades: el repositorio genérico necesita saber que cada
/// una tiene un Id entero para poder implementar GetById/Delete una sola vez.
/// </summary>
public abstract class EntidadBase
{
    public int Id { get; set; }
}
