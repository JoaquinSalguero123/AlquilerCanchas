using AlquilerCanchas.Infrastructure.Persistence;

namespace AlquilerCanchas.WebAPI.Configuracion;

/// <summary>
/// Corre migraciones y seeds al iniciar el host. Se hace en un IHostedService y no directo en Program.cs
/// para que `dotnet ef` (que construye el host pero nunca lo inicia) no intente conectarse a la base.
/// </summary>
public class InicializacionDeBaseDeDatos : IHostedService
{
    private readonly IServiceProvider _servicios;

    public InicializacionDeBaseDeDatos(IServiceProvider servicios) => _servicios = servicios;

    public Task StartAsync(CancellationToken ct) => InicializadorDeBaseDeDatos.EjecutarAsync(_servicios, ct);

    public Task StopAsync(CancellationToken ct) => Task.CompletedTask;
}
