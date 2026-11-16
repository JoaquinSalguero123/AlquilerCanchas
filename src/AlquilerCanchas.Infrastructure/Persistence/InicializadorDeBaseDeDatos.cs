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
        {
            var db = sp.GetRequiredService<AppDbContext>();
            await CargarCanchasDeEjemploAsync(db, ct);
            await CargarReservasDeEjemploAsync(db, sp.GetRequiredService<TimeProvider>(), log, ct);
        }
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

    /// <summary>
    /// Clientes, reservas y pagos para poder mostrar la API con datos (agenda, saldos, estados).
    /// Las fechas son relativas a hoy para que la agenda siempre tenga reservas próximas, y se cargan
    /// respetando las mismas reglas que los servicios (sin superposiciones y con pagos que no superan el total),
    /// porque el seed escribe directo en la base y nada lo valida.
    /// Sólo corre si no hay clientes: no pisa datos cargados a mano.
    /// </summary>
    private static async Task CargarReservasDeEjemploAsync(
        AppDbContext db, TimeProvider reloj, ILogger log, CancellationToken ct)
    {
        if (await db.Clientes.AnyAsync(ct)) return;

        var canchas = await db.Canchas.ToDictionaryAsync(c => c.Nombre, ct);
        string[] necesarias = ["Pádel 1", "Pádel 2", "Fútbol 5 A", "Tenis 1"];
        if (necesarias.Any(n => !canchas.ContainsKey(n)))
        {
            // Si alguien renombró las canchas de ejemplo no se adivinan: se omite el seed de reservas.
            log.LogWarning("No se cargaron reservas de ejemplo: faltan canchas con los nombres esperados.");
            return;
        }

        var hoy = DateOnly.FromDateTime(reloj.GetLocalNow().DateTime);
        var ana = new Cliente { Nombre = "Ana", Apellido = "Pérez", Email = "ana.perez@ejemplo.test", Telefono = "11 5555-0101" };
        var bruno = new Cliente { Nombre = "Bruno", Apellido = "Gómez", Email = "bruno.gomez@ejemplo.test", Telefono = "11 5555-0102" };
        var camila = new Cliente { Nombre = "Camila", Apellido = "Rodríguez", Email = "camila.rodriguez@ejemplo.test", Telefono = "11 5555-0103" };
        var diego = new Cliente { Nombre = "Diego", Apellido = "Fernández", Email = "diego.fernandez@ejemplo.test", Telefono = "11 5555-0104" };
        var lucia = new Cliente { Nombre = "Lucía", Apellido = "Martínez", Email = "lucia.martinez@ejemplo.test", Telefono = "11 5555-0105" };
        db.Clientes.AddRange(ana, bruno, camila, diego, lucia);

        Reserva R(Cliente c, string cancha, int dias, string inicio, string fin, EstadoReserva estado, params Pago[] pagos) => new()
        {
            Cliente = c,
            Cancha = canchas[cancha],
            Fecha = hoy.AddDays(dias),
            HoraInicio = TimeOnly.Parse(inicio),
            HoraFin = TimeOnly.Parse(fin),
            Estado = estado,
            Pagos = pagos
        };
        Pago P(decimal monto, MetodoPago metodo, int dias) =>
            new() { Monto = monto, MetodoPago = metodo, Fecha = hoy.AddDays(dias) };

        db.Reservas.AddRange(
            // Pasadas y saldadas: muestran el historial de cobros.
            R(ana, "Pádel 1", -3, "18:00", "19:30", EstadoReserva.Confirmada, P(27000m, MetodoPago.Efectivo, -3)),
            R(bruno, "Fútbol 5 A", -1, "21:00", "22:00", EstadoReserva.Confirmada,
                P(10000m, MetodoPago.Transferencia, -4), P(20000m, MetodoPago.Efectivo, -1)),
            // Próximas: confirmada con seña (queda saldo), pendientes y una cancelada.
            R(camila, "Pádel 1", 1, "18:00", "19:30", EstadoReserva.Confirmada, P(9000m, MetodoPago.MercadoPago, 0)),
            R(diego, "Pádel 2", 1, "19:30", "21:00", EstadoReserva.Pendiente),
            R(lucia, "Tenis 1", 2, "10:00", "11:00", EstadoReserva.Pendiente, P(4000m, MetodoPago.Transferencia, 0)),
            R(bruno, "Fútbol 5 A", 2, "20:00", "21:00", EstadoReserva.Cancelada),
            R(ana, "Pádel 1", 3, "18:00", "19:30", EstadoReserva.Pendiente));

        await db.SaveChangesAsync(ct);
        log.LogInformation("Se cargaron 5 clientes, 7 reservas y 5 pagos de ejemplo.");
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
