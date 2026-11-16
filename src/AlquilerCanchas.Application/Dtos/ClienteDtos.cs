using System.ComponentModel.DataAnnotations;

namespace AlquilerCanchas.Application.Dtos;

public record ClienteDto(int Id, string Nombre, string Apellido, string Email, string Telefono);

/// <summary>Cuerpo de alta y modificación; las anotaciones las valida [ApiController] antes de llegar al servicio.</summary>
public record ClienteGuardarDto(
    [Required, StringLength(80)] string Nombre,
    [Required, StringLength(80)] string Apellido,
    [Required, EmailAddress, StringLength(150)] string Email,
    [Required, StringLength(30)] string Telefono);
