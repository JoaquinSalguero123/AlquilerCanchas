using System.Text;
using AlquilerCanchas.Application.Dtos;
using AlquilerCanchas.Application.Interfaces;
using AlquilerCanchas.Domain.Entities;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace AlquilerCanchas.Infrastructure.Seguridad;

public class GeneradorDeTokensJwt : IGeneradorDeTokens
{
    private readonly JwtOpciones _opciones;
    private readonly TimeProvider _reloj;

    public GeneradorDeTokensJwt(IOptions<JwtOpciones> opciones, TimeProvider reloj)
    {
        _opciones = opciones.Value;
        _reloj = reloj;
    }

    public TokenDto Generar(Usuario usuario)
    {
        var expira = _reloj.GetUtcNow().UtcDateTime.AddMinutes(_opciones.ExpiraMinutos);
        var clave = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_opciones.Key));

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = _opciones.Issuer,
            Audience = _opciones.Audience,
            Expires = expira,
            // Claims con nombre corto ("role", "email") para que la validación no dependa
            // del mapeo de tipos de claim de .NET Framework.
            Claims = new Dictionary<string, object>
            {
                [JwtRegisteredClaimNames.Sub] = usuario.Id.ToString(),
                [JwtRegisteredClaimNames.Email] = usuario.Email,
                ["role"] = usuario.Rol.ToString()
            },
            SigningCredentials = new SigningCredentials(clave, SecurityAlgorithms.HmacSha256)
        };

        var token = new JsonWebTokenHandler().CreateToken(descriptor);
        return new TokenDto(token, expira, usuario.Email, usuario.Rol);
    }
}
