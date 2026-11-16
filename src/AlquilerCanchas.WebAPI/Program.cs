using System.Text;
using System.Text.Json.Serialization;
using AlquilerCanchas.Application;
using AlquilerCanchas.Infrastructure;
using AlquilerCanchas.Infrastructure.Seguridad;
using AlquilerCanchas.WebAPI.Configuracion;
using AlquilerCanchas.WebAPI.Middleware;
using Azure.Identity;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

// En Azure los secretos (Jwt--Key, cadena de conexión) viven en Key Vault. La API se autentica con su
// identidad administrada, así que no hay ninguna credencial guardada en el código ni en la configuración.
var urlKeyVault = builder.Configuration["KeyVault:Uri"];
if (!string.IsNullOrWhiteSpace(urlKeyVault))
    builder.Configuration.AddAzureKeyVault(new Uri(urlKeyVault), new DefaultAzureCredential());

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services
    .AddControllers()
    // Los enums viajan como texto ("Confirmada") y no como número, que obliga a consultar el código.
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

var jwt = builder.Configuration.GetSection(JwtOpciones.Seccion).Get<JwtOpciones>() ?? new JwtOpciones();
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o =>
    {
        // Sin mapeo automático: los claims llegan con el nombre con que se emitieron ("role", "email").
        o.MapInboundClaims = false;
        o.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwt.Issuer,
            ValidateAudience = true,
            ValidAudience = jwt.Audience,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            // Si la clave falta, AddInfrastructure ya abortó el arranque con un mensaje claro; el
            // valor vacío de acá sólo evita un NullReference en herramientas de diseño (migraciones).
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Key.PadRight(JwtOpciones.LargoMinimoDeKey))),
            ClockSkew = TimeSpan.FromMinutes(1),
            RoleClaimType = "role",
            NameClaimType = "email"
        };
    });
// Seguro por defecto: todo endpoint exige token salvo que se marque [AllowAnonymous].
builder.Services.AddAuthorizationBuilder()
    .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());

builder.Services.AddExceptionHandler<ManejadorDeExcepciones>();
builder.Services.AddProblemDetails();
builder.Services.AddHealthChecks();
builder.Services.AddHostedService<InicializacionDeBaseDeDatos>();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerConBearer();

var app = builder.Build();

app.UseExceptionHandler();

// Swagger queda abierto también en producción: es la forma de mostrar la API en la defensa oral.
app.UseSwagger();
app.UseSwaggerUI();

// Sin UseHttpsRedirection: en Azure el App Service ya fuerza HTTPS en su frontal (httpsOnly), y un segundo
// redireccionamiento detrás del proxy puede romper el health check, que llega por HTTP interno.
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/health").AllowAnonymous();

app.Run();

// Necesario para que los tests de integración puedan referenciar el punto de entrada.
public partial class Program;
