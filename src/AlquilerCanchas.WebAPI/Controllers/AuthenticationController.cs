using AlquilerCanchas.Application.Dtos;
using AlquilerCanchas.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AlquilerCanchas.WebAPI.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthenticationController : ControllerBase
{
    private readonly IAuthServicio _auth;

    public AuthenticationController(IAuthServicio auth) => _auth = auth;

    /// <summary>Valida las credenciales y devuelve un JWT.</summary>
    [HttpPost("login")]
    [AllowAnonymous]
    [ProducesResponseType<TokenDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<TokenDto>> Login(LoginDto dto, CancellationToken ct) =>
        Ok(await _auth.LoginAsync(dto, ct));

    /// <summary>
    /// Da de alta personal del complejo. Sólo un administrador puede hacerlo: si el registro
    /// fuera público, cualquiera podría crearse una cuenta y operar el sistema.
    /// </summary>
    [HttpPost("register")]
    [Authorize(Roles = "Administrador")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Register(RegistrarUsuarioDto dto, CancellationToken ct)
    {
        await _auth.RegistrarAsync(dto, ct);
        return NoContent();
    }
}
