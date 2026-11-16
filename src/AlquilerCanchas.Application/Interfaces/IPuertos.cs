using AlquilerCanchas.Application.Dtos;
using AlquilerCanchas.Domain.Entities;

namespace AlquilerCanchas.Application.Interfaces;

/// <summary>
/// Puertos hacia el mundo exterior. Application define el contrato y Infrastructure lo
/// implementa, así la lógica de negocio no conoce HttpClient, JWT ni PBKDF2.
/// </summary>
public record PronosticoHorario(DateTime HoraLocal, decimal TemperaturaC, int ProbabilidadLluviaPorcentaje);

public interface IProveedorDeClima
{
    /// <summary>Pronóstico hora a hora de un día. Devuelve lista vacía si el día está fuera del rango pronosticable.</summary>
    Task<IReadOnlyList<PronosticoHorario>> ObtenerPronosticoDelDiaAsync(DateOnly fecha, CancellationToken ct = default);
}

public interface IGeneradorDeTokens
{
    TokenDto Generar(Usuario usuario);
}

public interface IHasheadorDePasswords
{
    string Hashear(string password);
    bool Verificar(string password, string hash);
}
