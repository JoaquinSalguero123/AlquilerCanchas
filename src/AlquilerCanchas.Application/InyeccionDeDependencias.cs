using AlquilerCanchas.Application.Interfaces;
using AlquilerCanchas.Application.Services;
using Microsoft.Extensions.DependencyInjection;

namespace AlquilerCanchas.Application;

public static class InyeccionDeDependencias
{
    /// <summary>Registra los servicios de aplicación. Scoped: comparten el DbContext del request.</summary>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IClienteServicio, ClienteServicio>();
        services.AddScoped<ICanchaServicio, CanchaServicio>();
        services.AddScoped<IReservaServicio, ReservaServicio>();
        services.AddScoped<IPagoServicio, PagoServicio>();
        services.AddScoped<IAuthServicio, AuthServicio>();
        return services;
    }
}
