using AlquilerCanchas.Application.Interfaces;
using AlquilerCanchas.Domain.Interfaces;
using AlquilerCanchas.Infrastructure.Persistence;
using AlquilerCanchas.Infrastructure.Persistence.Repositorios;
using AlquilerCanchas.Infrastructure.Seguridad;
using AlquilerCanchas.Infrastructure.Servicios;
using AlquilerCanchas.Infrastructure.Servicios.Clima;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AlquilerCanchas.Infrastructure;

public static class InyeccionDeDependencias
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        var cadena = config.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Falta la cadena de conexión 'DefaultConnection'.");

        services.AddDbContext<AppDbContext>(o => o.UseSqlServer(cadena, sql =>
            // Azure SQL corta conexiones de forma transitoria (failover, throttling): reintentar es lo recomendado.
            sql.EnableRetryOnFailure(maxRetryCount: 3)));

        // Repositorio genérico abierto + los específicos que agregan consultas propias.
        services.AddScoped(typeof(IRepositorio<>), typeof(Repositorio<>));
        services.AddScoped<IClienteRepositorio, ClienteRepositorio>();
        services.AddScoped<ICanchaRepositorio, CanchaRepositorio>();
        services.AddScoped<IReservaRepositorio, ReservaRepositorio>();
        services.AddScoped<IPagoRepositorio, PagoRepositorio>();
        services.AddScoped<IUsuarioRepositorio, UsuarioRepositorio>();

        services.AddSingleton<TimeProvider, RelojArgentina>();
        services.AddSingleton<IHasheadorDePasswords, HasheadorPbkdf2>();
        services.AddScoped<IGeneradorDeTokens, GeneradorDeTokensJwt>();

        // Falla temprano si la clave JWT falta o es corta, en vez de explotar en el primer login.
        services.AddOptions<JwtOpciones>()
            .Bind(config.GetSection(JwtOpciones.Seccion))
            .Validate(o => o.Key.Length >= JwtOpciones.LargoMinimoDeKey,
                $"Jwt:Key debe tener al menos {JwtOpciones.LargoMinimoDeKey} caracteres. " +
                "Definila con user-secrets (local) o como variable de entorno / secreto de Key Vault (Azure).")
            .ValidateOnStart();

        services.Configure<ClimaOpciones>(config.GetSection(ClimaOpciones.Seccion));
        ConfigurarClienteDeClima(services, config);

        return services;
    }

    private static void ConfigurarClienteDeClima(IServiceCollection services, IConfiguration config)
    {
        var urlBase = config.GetSection(ClimaOpciones.Seccion)[nameof(ClimaOpciones.UrlBase)]
                      ?? new ClimaOpciones().UrlBase;

        // Cliente tipado vía IHttpClientFactory (requisito del TPI) + política estándar de resiliencia:
        // reintentos con espera, circuit breaker y timeouts, para que el clima caído no tumbe la API.
        services.AddHttpClient<IProveedorDeClima, OpenMeteoProveedorDeClima>(c =>
            {
                c.BaseAddress = new Uri(urlBase);
                c.DefaultRequestHeaders.UserAgent.ParseAdd("AlquilerCanchas-API/1.0");
            })
            .AddStandardResilienceHandler();
    }
}
