using System.ComponentModel.DataAnnotations;
using AlquilerCanchas.Domain.Enums;

namespace AlquilerCanchas.Application.Dtos;

public record LoginDto(
    [Required, EmailAddress] string Email,
    [Required] string Password);

public record RegistrarUsuarioDto(
    [Required, EmailAddress, StringLength(150)] string Email,
    [Required, StringLength(100, MinimumLength = 8)] string Password,
    RolUsuario Rol);

public record TokenDto(string Token, DateTime ExpiraUtc, string Email, RolUsuario Rol);
