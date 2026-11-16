# Imagen de la API (Azure Container Apps). Se construye desde la raíz del repositorio:
#   docker build -t alquilercanchas-api .

# ---- Compilación ----
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Primero sólo los .csproj: mientras no cambien las dependencias, Docker reutiliza esta capa
# y no vuelve a restaurar paquetes en cada build.
COPY src/AlquilerCanchas.Domain/*.csproj src/AlquilerCanchas.Domain/
COPY src/AlquilerCanchas.Application/*.csproj src/AlquilerCanchas.Application/
COPY src/AlquilerCanchas.Infrastructure/*.csproj src/AlquilerCanchas.Infrastructure/
COPY src/AlquilerCanchas.WebAPI/*.csproj src/AlquilerCanchas.WebAPI/
RUN dotnet restore src/AlquilerCanchas.WebAPI

COPY src/ src/
RUN dotnet publish src/AlquilerCanchas.WebAPI -c Release -o /app --no-restore

# ---- Ejecución ----
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app .

# Las imágenes de .NET corren con un usuario sin privilegios y escuchan en el 8080 por defecto.
# El puerto coincide con targetPort de la Container App (infra/main.bicep).
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "AlquilerCanchas.WebAPI.dll"]
