# Alquiler de Canchas — API

TPI de Programación IV (UTN TUP). API REST en **ASP.NET Core 10** para gestionar las canchas de un
complejo deportivo, sus clientes, las reservas y los pagos. Parte del diagrama de clases del TP1
(`Cliente`, `Cancha`, `Reserva`, `Pago`) y la minuta de relevamiento.

## Arquitectura (Clean Architecture)

```
src/
  AlquilerCanchas.Domain          Entidades, enums, excepciones e interfaces de repositorio. No depende de nada.
  AlquilerCanchas.Application     Servicios de negocio, DTOs y puertos (IProveedorDeClima, IGeneradorDeTokens...).
  AlquilerCanchas.Infrastructure  EF Core (DbContext, migraciones, repositorios), JWT, PBKDF2 y cliente de Open-Meteo.
  AlquilerCanchas.WebAPI          Controladores, autenticación, Swagger, manejo de errores y Program.cs.
tests/
  AlquilerCanchas.Tests           Pruebas unitarias (xUnit + NSubstitute).
  prueba-api.sh                   Prueba de punta a punta contra una API levantada.
infra/main.bicep                  Container Apps + Container Registry + Azure SQL + Key Vault.
Dockerfile                        Imagen de la API.
.github/workflows/ci-cd.yml       Pipeline de GitHub Actions: compila, prueba, crea infraestructura y despliega.
```

Flujo de una petición: `Controller → Servicio (Application) → Repositorio (interfaz en Domain, implementación
en Infrastructure) → AppDbContext → SQL Server`. Las dependencias apuntan hacia adentro y todo se inyecta
por constructor.

## Modelo

| Entidad | Atributos (diagrama aprobado) | Relaciones |
|---|---|---|
| Cliente | nombre, apellido, email, telefono | 1 — N Reserva |
| Cancha | nombre, tipoDeporte, precioPorHora | 1 — N Reserva |
| Reserva | fecha, horaInicio, horaFin, estado | N — 1 Cliente, N — 1 Cancha, 1 — N Pago |
| Pago | monto, fecha, metodoPago | N — 1 Reserva |
| **Usuario** | email, passwordHash, rol | *No está en el diagrama.* |

`Usuario` es la **única entidad agregada**: hace falta para que el login JWT tenga contra qué validar. Si la
cátedra no la acepta, la alternativa es mantener las credenciales del personal en configuración.

## Reglas de negocio

- Una cancha no puede tener dos reservas **no canceladas** que se pisen el mismo día. Dos reservas contiguas
  (una termina 19:30 y otra empieza 19:30) sí se permiten.
- Estados de la reserva: `Pendiente → Confirmada`, y `Pendiente/Confirmada → Cancelada`. Sólo se modifican las pendientes.
- El total de una reserva = horas × `precioPorHora` de la cancha (no se guarda; así el diagrama no cambia).
- Los pagos no pueden superar el saldo, ni registrarse sobre reservas canceladas. La fecha del pago la pone el servidor (hora argentina).
- No se borran clientes ni canchas con reservas (se conserva el historial).
- `GET /api/reservas/{id}/clima` consulta **Open-Meteo** (servicio de terceros, sin clave) mediante un cliente tipado de
  `IHttpClientFactory` con política de resiliencia, y recomienda suspender si la probabilidad de lluvia es ≥ 60 %.

## Endpoints

Todos exigen `Authorization: Bearer <token>` salvo `login` y `/health`. Swagger UI en `/swagger`.

| Método | Ruta | Rol |
|---|---|---|
| POST | `/api/auth/login` | público |
| POST | `/api/auth/register` | Administrador |
| GET / POST / PUT | `/api/clientes[/{id}]` | cualquiera |
| DELETE | `/api/clientes/{id}` | Administrador |
| GET | `/api/canchas[/{id}]` | cualquiera |
| POST / PUT / DELETE | `/api/canchas[/{id}]` | Administrador |
| GET | `/api/reservas?fecha=&canchaId=&clienteId=` | cualquiera |
| GET / POST / PUT | `/api/reservas[/{id}]` | cualquiera |
| POST | `/api/reservas/{id}/confirmar` · `/cancelar` | cualquiera |
| GET | `/api/reservas/{id}/clima` | cualquiera |
| GET / POST | `/api/reservas/{id}/pagos` | cualquiera |
| DELETE | `/api/pagos/{id}` | Administrador |
| GET | `/health` | público |

## Correr en local

Requisitos: SDK de .NET 10 y SQL Server LocalDB (viene con Visual Studio) o cualquier SQL Server.

```bash
cd src/AlquilerCanchas.WebAPI
dotnet user-secrets init
dotnet user-secrets set "Jwt:Key" "<una cadena aleatoria de 32+ caracteres>"
dotnet user-secrets set "Seed:AdminEmail" "admin@canchas.test"
dotnet user-secrets set "Seed:AdminPassword" "<tu contraseña>"
dotnet run
```

La clave JWT **no está en `appsettings.json`** a propósito: en local vive en user-secrets y en Azure en Key Vault.
En `Development` la API aplica las migraciones sola, crea el administrador y carga 4 canchas de ejemplo.
Cadena de conexión local: `appsettings.Development.json` (LocalDB); para otra base, pisá `ConnectionStrings:DefaultConnection`.

```bash
dotnet test                                                        # pruebas unitarias
ADMIN_EMAIL=... ADMIN_PASSWORD=... ./tests/prueba-api.sh http://localhost:5165   # prueba de punta a punta
```

Migraciones (Code First):

```bash
dotnet tool restore
dotnet ef migrations add <Nombre> --project src/AlquilerCanchas.Infrastructure --startup-project src/AlquilerCanchas.WebAPI --output-dir Persistence/Migrations
```

## Despliegue en Azure (CI/CD)

**URL pública:** <https://ca-alqcanchas.purplewater-44d39abd.brazilsouth.azurecontainerapps.io> (Swagger en `/swagger`).

La API corre como contenedor en **Azure Container Apps** (HTTPS y dominio público incluidos), con imagen en
**Azure Container Registry**, base en **Azure SQL** y secretos en **Key Vault**. Se eligió Container Apps y no App Service
porque las suscripciones de prueba gratuita nuevas llegan con cuota 0 de App Service; la variante con App Service
quedó en `infra/main.appservice.bicep`.

El pipeline hace: compilar → probar → (en `main`) crear la infraestructura con Bicep → construir y subir la imagen de
Docker → crear/actualizar la Container App → verificar `/health`. La API usa una identidad administrada para bajar la
imagen y leer los secretos de Key Vault (`Jwt--Key`, `ConnectionStrings--DefaultConnection`, `Seed--*`); en la
Container App no hay ningún secreto en claro. La rama `test` sólo corre compilación y pruebas.

Configuración única:

1. Crear un grupo de recursos: `az group create -n rg-alquiler-canchas -l brazilsouth`, y registrar los servicios
   `Microsoft.App`, `Microsoft.ContainerRegistry`, `Microsoft.ManagedIdentity`, `Microsoft.OperationalInsights`,
   `Microsoft.Sql`, `Microsoft.KeyVault` (`az provider register --namespace ...`).
2. Crear un App Registration con **credencial federada** para GitHub (OIDC). GitHub firma el token con el subject
   `repo:<usuario>@<id>/<repo>@<id>:environment:produccion` (con los IDs numéricos): hay que registrar exactamente ese
   (el error `AADSTS700213` del login lo muestra). Darle rol **Owner** sobre el grupo de recursos: hace falta para
   asignar los roles de la API sobre Key Vault y el registro.
3. En GitHub → Settings → Secrets and variables → Actions:
   - Secrets: `AZURE_CLIENT_ID`, `AZURE_TENANT_ID`, `AZURE_SUBSCRIPTION_ID`, `SQL_ADMIN_PASSWORD`, `JWT_KEY`, `ADMIN_PASSWORD`
   - Variables: `AZURE_RESOURCE_GROUP`, `ADMIN_EMAIL`
   - (Las contraseñas sin espacios: se pasan como parámetros de línea de comandos.)
4. Crear el *environment* `produccion` en GitHub y hacer push a `main`.

## Checklist del TPI

| Requisito | Dónde |
|---|---|
| Clean Architecture (Domain, Application, Infrastructure, WebAPI) | `src/` |
| Entidades según el diagrama aprobado | `Domain/Entities` |
| Repositorios + patrón genérico | `Domain/Interfaces/IRepositorios.cs`, `Infrastructure/Persistence/Repositorios` |
| Servicios inyectados con repositorios | `Application/Services`, `Application/InyeccionDeDependencias.cs` |
| DbContext + Program.cs | `Infrastructure/Persistence/AppDbContext.cs`, `WebAPI/Program.cs` |
| Migrations + Snapshot (Code First) | `Infrastructure/Persistence/Migrations` |
| Controladores + `AuthenticationController` | `WebAPI/Controllers` |
| Persistencia relacional operativa | SQL Server / Azure SQL |
| Servicio de terceros con HttpClientFactory | `Infrastructure/Servicios/Clima` |
| JWT funcional, secret fuera del repo | `Infrastructure/Seguridad`, Key Vault |
| Pipeline GitHub Actions → Azure | `.github/workflows/ci-cd.yml`, `infra/main.bicep` |
