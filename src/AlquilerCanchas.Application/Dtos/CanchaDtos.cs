using System.ComponentModel.DataAnnotations;

namespace AlquilerCanchas.Application.Dtos;

public record CanchaDto(int Id, string Nombre, string TipoDeporte, decimal PrecioPorHora);

public record CanchaGuardarDto(
    [Required, StringLength(80)] string Nombre,
    [Required, StringLength(50)] string TipoDeporte,
    [Range(0.01, 1_000_000)] decimal PrecioPorHora);
