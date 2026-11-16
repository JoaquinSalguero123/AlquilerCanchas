using Microsoft.OpenApi;

namespace AlquilerCanchas.WebAPI.Configuracion;

public static class SwaggerConBearer
{
    /// <summary>Agrega el botón "Authorize" de Swagger UI para probar los endpoints protegidos con el JWT.</summary>
    public static IServiceCollection AddSwaggerConBearer(this IServiceCollection services)
    {
        services.AddSwaggerGen(o =>
        {
            o.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "Alquiler de Canchas API",
                Version = "v1",
                Description = "TPI Programación IV - UTN. Gestión de canchas, clientes, reservas y pagos."
            });

            o.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                Description = "Pegá el token que devuelve POST /api/auth/login (sin la palabra 'Bearer')."
            });

            o.AddSecurityRequirement(doc => new OpenApiSecurityRequirement
            {
                [new OpenApiSecuritySchemeReference("Bearer", doc)] = []
            });
        });
        return services;
    }
}
