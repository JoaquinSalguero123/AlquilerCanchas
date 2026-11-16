using AlquilerCanchas.Application.Dtos;
using AlquilerCanchas.Application.Interfaces;
using AlquilerCanchas.Application.Services;
using AlquilerCanchas.Domain.Entities;
using AlquilerCanchas.Domain.Enums;
using AlquilerCanchas.Domain.Exceptions;
using AlquilerCanchas.Domain.Interfaces;
using AlquilerCanchas.Infrastructure.Seguridad;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using NSubstitute;
using System.Text;

namespace AlquilerCanchas.Tests;

public class SeguridadTests
{
    private const string ClaveDePrueba = "clave-de-prueba-de-al-menos-32-caracteres!";

    [Fact]
    public void El_hash_verifica_la_clave_correcta_y_rechaza_otra()
    {
        var hasheador = new HasheadorPbkdf2();
        var hash = hasheador.Hashear("Secreta123");

        Assert.True(hasheador.Verificar("Secreta123", hash));
        Assert.False(hasheador.Verificar("secreta123", hash));
    }

    [Fact]
    public void Dos_hashes_de_la_misma_clave_difieren_por_la_sal()
    {
        var hasheador = new HasheadorPbkdf2();
        Assert.NotEqual(hasheador.Hashear("Secreta123"), hasheador.Hashear("Secreta123"));
    }

    [Fact]
    public void Un_hash_con_formato_roto_no_verifica_en_lugar_de_explotar()
    {
        Assert.False(new HasheadorPbkdf2().Verificar("x", "esto-no-es-un-hash"));
    }

    [Fact]
    public async Task El_token_emitido_se_valida_con_la_misma_clave_e_incluye_el_rol()
    {
        var opciones = Options.Create(new JwtOpciones { Key = ClaveDePrueba, ExpiraMinutos = 30 });
        var generador = new GeneradorDeTokensJwt(opciones, TimeProvider.System);

        var token = generador.Generar(new Usuario { Id = 3, Email = "a@b.test", Rol = RolUsuario.Administrador });

        var resultado = await new JsonWebTokenHandler().ValidateTokenAsync(token.Token, new TokenValidationParameters
        {
            ValidIssuer = opciones.Value.Issuer,
            ValidAudience = opciones.Value.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(ClaveDePrueba))
        });

        Assert.True(resultado.IsValid);
        Assert.Equal("Administrador", resultado.Claims["role"]);
        Assert.Equal("a@b.test", resultado.Claims["email"]);
    }

    [Fact]
    public async Task Un_token_firmado_con_otra_clave_no_valida()
    {
        var generador = new GeneradorDeTokensJwt(
            Options.Create(new JwtOpciones { Key = ClaveDePrueba }), TimeProvider.System);
        var token = generador.Generar(new Usuario { Id = 1, Email = "a@b.test" });

        var resultado = await new JsonWebTokenHandler().ValidateTokenAsync(token.Token, new TokenValidationParameters
        {
            ValidIssuer = "AlquilerCanchas.API",
            ValidAudience = "AlquilerCanchas.Clientes",
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes("otra-clave-distinta-de-32-caracteres!!"))
        });

        Assert.False(resultado.IsValid);
    }

    [Fact]
    public async Task Login_con_email_inexistente_y_con_clave_incorrecta_dan_el_mismo_error()
    {
        var usuarios = Substitute.For<IUsuarioRepositorio>();
        var hasheador = new HasheadorPbkdf2();
        usuarios.ObtenerPorEmailAsync("existe@b.test", Arg.Any<CancellationToken>())
            .Returns(new Usuario { Email = "existe@b.test", PasswordHash = hasheador.Hashear("Correcta123") });
        var auth = new AuthServicio(usuarios, hasheador, Substitute.For<IGeneradorDeTokens>());

        var inexistente = await Assert.ThrowsAsync<CredencialesInvalidasException>(
            () => auth.LoginAsync(new LoginDto("nadie@b.test", "x")));
        var incorrecta = await Assert.ThrowsAsync<CredencialesInvalidasException>(
            () => auth.LoginAsync(new LoginDto("existe@b.test", "Incorrecta")));

        // Mismo mensaje: la respuesta no revela qué emails están registrados.
        Assert.Equal(inexistente.Message, incorrecta.Message);
    }
}
