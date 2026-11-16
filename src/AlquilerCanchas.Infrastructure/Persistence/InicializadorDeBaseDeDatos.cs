using AlquilerCanchas.Application.Interfaces;
using AlquilerCanchas.Domain.Entities;
using AlquilerCanchas.Domain.Enums;
using AlquilerCanchas.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AlquilerCanchas.Infrastructure.Persistence;

/// <summary>Aplica migraciones y carga los datos mínimos al arrancar la API.</summary>
public static class InicializadorDeBaseDeDatos
{
    public static async Task EjecutarAsync(IServiceProvider servicios, CancellationToken ct = default)
    {
        using var scope = servicios.CreateScope();
        var sp = scope.ServiceProvider;
        var config = sp.GetRequiredService<IConfiguration>();
        var log = sp.GetRequiredService<ILoggerFactory>().CreateLogger("InicializadorDeBaseDeDatos");

        // En Azure el despliegue deja la API lista sin pasos manuales: la API se migra sola al iniciar.
        if (config.GetValue("Database:MigrarAlIniciar", false))
        {
            log.LogInformation("Aplicando migraciones pendientes...");
            await sp.GetRequiredService<AppDbContext>().Database.MigrateAsync(ct);
        }

        await CrearAdministradorInicialAsync(sp, config, log, ct);

        if (config.GetValue("Seed:DatosDeEjemplo", false))
            await CargarCanchasDeEjemploAsync(sp.GetRequiredService<AppDbContext>(), ct);
    }

    /// <summary>
    /// Sin un primer usuario nadie podría entrar (el alta de usuarios exige ser administrador).
    /// Las credenciales llegan por configuración —nunca fijas en el código— y sólo se usan si no hay usuarios.
    /// </summary>
    private static async Task CrearAdministradorInicialAsync(
        IServiceProvider sp, IConfiguration config, ILogger log, CancellationToken ct)
    {
        var email = config["Seed:AdminEmail"];
        var password = config["Seed:AdminPassword"];
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password)) return;

        var usuarios = sp.GetRequiredService<IUsuarioRepositorio>();
        if (await usuarios.ExistenUsuariosAsync(ct)) return;

        await usuarios.AgregarAsync(new Usuario
        {
            Email = email.Trim(),
            PasswordHash = sp.GetRequiredService<IHasheadorDePasswords>().Hashear(password),
            Rol = RolUsuario.Administrador
        }, ct);
        await usuarios.GuardarCambiosAsync(ct);
        log.LogInformation("Se creó el administrador inicial {Email}.", email);
    }

    private static async Task CargarCanchasDeEjemploAsync(AppDbContext db, CancellationToken ct)
    {
        if (await db.Canchas.AnyAsync(ct)) return;

        db.Canchas.AddRange(
            new Cancha { Nombre = "Pádel 1", TipoDeporte = "Pádel", PrecioPorHora = 18000m },
            new Cancha { Nombre = "Pádel 2", TipoDeporte = "Pádel", PrecioPorHora = 18000m },
            new Cancha { Nombre = "Fútbol 5 A", TipoDeporte = "Fútbol 5", PrecioPorHora = 30000m },
            new Cancha { Nombre = "Tenis 1", TipoDeporte = "Tenis", PrecioPorHora = 12000m });
        await db.SaveChangesAsync(ct);
    }
}
