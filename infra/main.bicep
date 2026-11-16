// Infraestructura en Azure: Container Apps (la API en un contenedor) + Azure Container Registry + Azure SQL + Key Vault.
//
// Se eligió Container Apps y no App Service porque las suscripciones de prueba gratuita nuevas llegan con
// cuota 0 de App Service en todas las regiones. La alternativa con App Service quedó en main.appservice.bicep
// por si la suscripción se pasa a "pago por uso".
//
// Se despliega DOS veces desde el pipeline con la misma plantilla:
//   1) imagen = ''   -> crea ACR, SQL, Key Vault, identidad y permisos (todavía no hay imagen que correr).
//   2) imagen = <tag> -> con la imagen ya subida al ACR, crea/actualiza la Container App.

@description('Prefijo de los nombres de recursos (minúsculas y números).')
@minLength(3)
@maxLength(10)
param nombreApp string = 'alqcanchas'

@description('Región. Por defecto, la del grupo de recursos.')
param ubicacion string = resourceGroup().location

@description('Imagen completa a desplegar (acr/repositorio:tag). Vacía = sólo infraestructura, sin Container App.')
param imagen string = ''

@description('Réplicas mínimas. 1 evita el arranque en frío (importante el día de la defensa); 0 ahorra crédito.')
@minValue(0)
@maxValue(2)
param replicasMinimas int = 1

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

// Los nombres de SQL, Key Vault y ACR son únicos a nivel global: el sufijo sale del grupo de recursos.
var sufijo = uniqueString(resourceGroup().id)
var nombreSql = 'sql-${nombreApp}-${sufijo}'
var nombreBase = 'AlquilerCanchas'
var nombreKv = 'kv-${take(nombreApp, 8)}-${take(sufijo, 8)}'
var nombreAcr = 'acr${nombreApp}${take(sufijo, 8)}'

// Roles integrados de Azure.
var rolLectorDeSecretos = '4633458b-17de-408a-b874-0445c86b69e6' // Key Vault Secrets User
var rolAcrPull = '7f951dda-4ed3-4680-a7ca-43fe172d538d' // AcrPull

// Identidad propia de la API (creada ANTES que la app): así sus permisos ya existen cuando la Container App
// arranca y tiene que bajar la imagen y leer los secretos. Con identidad automática habría una carrera.
resource identidad 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
  name: 'id-${nombreApp}'
  location: ubicacion
}

resource acr 'Microsoft.ContainerRegistry/registries@2023-07-01' = {
  name: nombreAcr
  location: ubicacion
  sku: { name: 'Basic' }
  properties: { adminUserEnabled: false }
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

// Habilita que los servicios de Azure (la Container App) lleguen al servidor SQL.
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

resource permisoSecretos 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  scope: kv
  name: guid(kv.id, identidad.id, rolLectorDeSecretos)
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', rolLectorDeSecretos)
    principalId: identidad.properties.principalId
    principalType: 'ServicePrincipal'
  }
}

resource permisoImagenes 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  scope: acr
  name: guid(acr.id, identidad.id, rolAcrPull)
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', rolAcrPull)
    principalId: identidad.properties.principalId
    principalType: 'ServicePrincipal'
  }
}

resource logs 'Microsoft.OperationalInsights/workspaces@2023-09-01' = {
  name: 'log-${nombreApp}'
  location: ubicacion
  properties: {
    sku: { name: 'PerGB2018' }
    retentionInDays: 30
  }
}

resource entorno 'Microsoft.App/managedEnvironments@2024-03-01' = {
  name: 'cae-${nombreApp}'
  location: ubicacion
  properties: {
    appLogsConfiguration: {
      destination: 'log-analytics'
      logAnalyticsConfiguration: {
        customerId: logs.properties.customerId
        sharedKey: logs.listKeys().primarySharedKey
      }
    }
  }
}

resource api 'Microsoft.App/containerApps@2024-03-01' = if (!empty(imagen)) {
  name: 'ca-${nombreApp}'
  location: ubicacion
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: { '${identidad.id}': {} }
  }
  // Los permisos tienen que existir antes de que la app intente bajar la imagen y leer secretos.
  dependsOn: [permisoImagenes, permisoSecretos, secretoJwt, secretoConexion, secretoAdminEmail, secretoAdminPassword]
  properties: {
    managedEnvironmentId: entorno.id
    configuration: {
      // HTTPS y dominio público (*.azurecontainerapps.io) los da el propio servicio.
      ingress: {
        external: true
        targetPort: 8080
        transport: 'auto'
        allowInsecure: false
      }
      registries: [
        { server: acr.properties.loginServer, identity: identidad.id }
      ]
    }
    template: {
      containers: [
        {
          name: 'api'
          image: imagen
          resources: { cpu: json('0.25'), memory: '0.5Gi' }
          env: [
            { name: 'ASPNETCORE_ENVIRONMENT', value: 'Production' }
            // Apunta al vault: lo único que la API necesita para ir a buscar sus secretos.
            { name: 'KeyVault__Uri', value: kv.properties.vaultUri }
            // Con una identidad de usuario, DefaultAzureCredential necesita saber cuál usar.
            { name: 'AZURE_CLIENT_ID', value: identidad.properties.clientId }
            // La API aplica las migraciones al iniciar: el deploy deja la base lista.
            { name: 'Database__MigrarAlIniciar', value: 'true' }
          ]
          probes: [
            { type: 'Liveness', httpGet: { path: '/health', port: 8080 }, initialDelaySeconds: 20, periodSeconds: 30 }
            { type: 'Readiness', httpGet: { path: '/health', port: 8080 }, initialDelaySeconds: 10, periodSeconds: 10 }
          ]
        }
      ]
      scale: { minReplicas: replicasMinimas, maxReplicas: 2 }
    }
  }
}

output nombreAcr string = acr.name
output servidorAcr string = acr.properties.loginServer
// El "!" es seguro: sólo se evalúa cuando imagen no está vacía, que es justo cuando la Container App existe.
output urlApi string = !empty(imagen) ? 'https://${api!.properties.configuration.ingress.fqdn}' : ''
output urlKeyVault string = kv.properties.vaultUri
