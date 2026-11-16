using AlquilerCanchas.Application.Dtos;
using AlquilerCanchas.Application.Interfaces;
using AlquilerCanchas.Domain.Entities;
using AlquilerCanchas.Domain.Exceptions;
using AlquilerCanchas.Domain.Interfaces;

namespace AlquilerCanchas.Application.Services;

public class AuthServicio : IAuthServicio
{
    private readonly IUsuarioRepositorio _usuarios;
    private readonly IHasheadorDePasswords _hasheador;
    private readonly IGeneradorDeTokens _tokens;

    public AuthServicio(IUsuarioRepositorio usuarios, IHasheadorDePasswords hasheador, IGeneradorDeTokens tokens)
    {
        _usuarios = usuarios;
        _hasheador = hasheador;
        _tokens = tokens;
    }

    public async Task<TokenDto> LoginAsync(LoginDto dto, CancellationToken ct = default)
    {
        var usuario = await _usuarios.ObtenerPorEmailAsync(dto.Email.Trim(), ct);

        // Mismo error para "no existe" y "clave incorrecta": no revelar qué emails están registrados.
        if (usuario is null || !_hasheador.Verificar(dto.Password, usuario.PasswordHash))
            throw new CredencialesInvalidasException();

        return _tokens.Generar(usuario);
    }

    public async Task RegistrarAsync(RegistrarUsuarioDto dto, CancellationToken ct = default)
    {
        var email = dto.Email.Trim();
        if (await _usuarios.ObtenerPorEmailAsync(email, ct) is not null)
            throw new ReglaDeNegocioException($"Ya existe un usuario con el email {email}.");

        await _usuarios.AgregarAsync(new Usuario
        {
            Email = email,
            PasswordHash = _hasheador.Hashear(dto.Password),
            Rol = dto.Rol
        }, ct);
        await _usuarios.GuardarCambiosAsync(ct);
    }
}
