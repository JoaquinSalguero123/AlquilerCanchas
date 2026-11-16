// Infraestructura de la API en Azure: App Service (Linux, .NET 10) + Azure SQL + Key Vault.
// Se despliega desde el pipeline (.github/workflows/ci-cd.yml) y es idempotente: correrlo de nuevo
// sólo actualiza lo que cambió.

@description('Prefijo de los nombres de recursos (minúsculas, sin guiones bajos).')
@minLength(3)
@maxLength(12)
param nombreApp string = 'alqcanchas'

@description('Región. Por defecto, la del grupo de recursos.')
param ubicacion string = resourceGroup().location

@description('Plan del App Service. B1 alcanza para la defensa; F1 es gratis pero se duerme.')
param skuPlan string = 'B1'

@description('Usuario administrador del servidor SQL.')
param sqlAdminUsuario string = 'sqladmin'

@secure()
@description('Contraseña del administrador SQL. Llega desde un secreto de GitHub, nunca del repositorio.')
param sqlAdminPassword string

@secure()
@description('Clave de firma de los JWT (mínimo 32 caracteres).')
param jwtKey string

@description('Email del administrador inicial de la aplicación.')
param adminEmail string

@secure()
@description('Contraseña del administrador inicial de la aplicación.')
param adminPassword string

// Sufijo estable derivado del grupo de recursos: los nombres de SQL, Key Vault y App Service
// son únicos a nivel global y así no chocan con los de otras personas.
var sufijo = uniqueString(resourceGroup().id)
var nombrePlan = 'plan-${nombreApp}'
var nombreWeb = '${nombreApp}-${sufijo}'
var nombreSql = 'sql-${nombreApp}-${sufijo}'
var nombreBase = 'AlquilerCanchas'
var nombreKv = 'kv-${take(nombreApp, 8)}-${take(sufijo, 8)}'

// Roles integrados: Key Vault Secrets User (leer secretos).
var rolLectorDeSecretos = '4633458b-17de-408a-b874-0445c86b69e6'

resource plan 'Microsoft.Web/serverfarms@2023-12-01' = {
  name: nombrePlan
  location: ubicacion
  kind: 'linux'
  sku: { name: skuPlan }
  properties: { reserved: true }
}

resource sql 'Microsoft.Sql/servers@2023-08-01-preview' = {
  name: nombreSql
  location: ubicacion
  properties: {
    administratorLogin: sqlAdminUsuario
    administratorLoginPassword: sqlAdminPassword
    minimalTlsVersion: '1.2'
  }
}

resource baseDeDatos 'Microsoft.Sql/servers/databases@2023-08-01-preview' = {
  parent: sql
  name: nombreBase
  location: ubicacion
  sku: { name: 'Basic', tier: 'Basic' }
}

// Habilita que los servicios de Azure (el App Service) lleguen al servidor SQL.
resource reglaAzure 'Microsoft.Sql/servers/firewallRules@2023-08-01-preview' = {
  parent: sql
  name: 'PermitirServiciosDeAzure'
  properties: { startIpAddress: '0.0.0.0', endIpAddress: '0.0.0.0' }
}

resource kv 'Microsoft.KeyVault/vaults@2023-07-01' = {
  name: nombreKv
  location: ubicacion
  properties: {
    sku: { family: 'A', name: 'standard' }
    tenantId: subscription().tenantId
    enableRbacAuthorization: true
    enableSoftDelete: true
  }
}

// En Key Vault el separador de secciones de configuración es "--" (el ":" no es válido en un nombre).
resource secretoJwt 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  parent: kv
  name: 'Jwt--Key'
  properties: { value: jwtKey }
}

resource secretoConexion 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  parent: kv
  name: 'ConnectionStrings--DefaultConnection'
  properties: {
    value: 'Server=tcp:${sql.properties.fullyQualifiedDomainName},1433;Initial Catalog=${nombreBase};User ID=${sqlAdminUsuario};Password=${sqlAdminPassword};Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;'
  }
}

resource secretoAdminEmail 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  parent: kv
  name: 'Seed--AdminEmail'
  properties: { value: adminEmail }
}

resource secretoAdminPassword 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  parent: kv
  name: 'Seed--AdminPassword'
  properties: { value: adminPassword }
}

resource web 'Microsoft.Web/sites@2023-12-01' = {
  name: nombreWeb
  location: ubicacion
  // Identidad administrada: la API entra a Key Vault sin ninguna credencial guardada.
  identity: { type: 'SystemAssigned' }
  properties: {
    serverFarmId: plan.id
    httpsOnly: true
    siteConfig: {
      linuxFxVersion: 'DOTNETCORE|10.0'
      alwaysOn: skuPlan != 'F1'
      healthCheckPath: '/health'
      minTlsVersion: '1.2'
      ftpsState: 'Disabled'
      appSettings: [
        { name: 'ASPNETCORE_ENVIRONMENT', value: 'Production' }
        // Apunta al vault: el único dato que la API necesita para ir a buscar sus secretos.
        { name: 'KeyVault__Uri', value: kv.properties.vaultUri }
        // La API aplica las migraciones al iniciar, así el deploy deja la base lista.
        { name: 'Database__MigrarAlIniciar', value: 'true' }
      ]
    }
  }
}

resource permisoLecturaSecretos 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  scope: kv
  name: guid(kv.id, web.id, rolLectorDeSecretos)
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', rolLectorDeSecretos)
    principalId: web.identity.principalId
    principalType: 'ServicePrincipal'
  }
}

output nombreWebApp string = web.name
output urlApi string = 'https://${web.properties.defaultHostName}'
output urlKeyVault string = kv.properties.vaultUri
