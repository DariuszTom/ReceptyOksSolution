// =====================================================
// ReceptyOks API - Azure Infrastructure (Bicep)
// =====================================================
// Ten plik tworzy całą infrastrukturę dla backendu:
// - Azure SQL Database (Basic tier)
// - Container App Environment
// - Container App (receptyoks-api)
// - Key Vault (dla sekretów)
//
// Deployment:
//   az deployment group create \
//     --resource-group <RG> \
//     --template-file main.bicep \
//     --parameters main.bicepparam
// =====================================================

@description('Lokalizacja dla wszystkich zasobów')
param location string = resourceGroup().location

@description('Nazwa aplikacji (używana jako prefix)')
param appName string = 'receptyoks'

@description('Nazwa obrazu Docker (bez tagu)')
param containerImage string

@description('Tag obrazu Docker')
param containerImageTag string = 'latest'

@description('Nazwa serwera ACR (np. myacr.azurecr.io)')
param acrServer string

@description('Klucz JWT do autentykacji API')
@secure()
param jwtKey string

@description('Klucz API dla dostępu do endpointów')
@secure()
param apiKey string

@description('Hasło administratora SQL')
@secure()
param sqlAdminPassword string

@description('Login administratora SQL')
param sqlAdminLogin string = 'receptyoksadmin'

// =====================================================
// Azure SQL Database
// =====================================================
resource sqlServer 'Microsoft.Sql/servers@2023-05-01-preview' = {
  name: '${appName}-sql'
  location: location
  properties: {
    administratorLogin: sqlAdminLogin
    administratorLoginPassword: sqlAdminPassword
    version: '12.0'
    minimalTlsVersion: '1.2'
    publicNetworkAccess: 'Enabled'
  }
}

resource sqlDatabase 'Microsoft.Sql/servers/databases@2023-05-01-preview' = {
  parent: sqlServer
  name: '${appName}db'
  location: location
  sku: {
    name: 'Basic'
    tier: 'Basic'
    capacity: 5
  }
  properties: {
    collation: 'SQL_Latin1_General_CP1_CI_AS'
    maxSizeBytes: 2147483648 // 2 GB
  }
}

// Firewall rule - Allow Azure Services
resource sqlFirewallAzure 'Microsoft.Sql/servers/firewallRules@2023-05-01-preview' = {
  parent: sqlServer
  name: 'AllowAzureServices'
  properties: {
    startIpAddress: '0.0.0.0'
    endIpAddress: '0.0.0.0'
  }
}

// =====================================================
// Log Analytics Workspace (opcjonalny - dla logów)
// =====================================================
@description('Czy utworzyć Log Analytics? (false = tańsza opcja)')
param enableLogAnalytics bool = false

resource logAnalytics 'Microsoft.OperationalInsights/workspaces@2022-10-01' = if (enableLogAnalytics) {
  name: '${appName}-logs'
  location: location
  properties: {
    sku: {
      name: 'PerGB2018'
    }
    retentionInDays: 30
  }
}

// =====================================================
// Container App Environment
// =====================================================
resource containerAppEnv 'Microsoft.App/managedEnvironments@2024-03-01' = {
  name: '${appName}-env'
  location: location
  properties: {
    appLogsConfiguration: enableLogAnalytics ? {
      destination: 'log-analytics'
      logAnalyticsConfiguration: {
        customerId: logAnalytics.properties.customerId
        sharedKey: logAnalytics.listKeys().primarySharedKey
      }
    } : {
      destination: 'azure-monitor'
    }
  }
}

// =====================================================
// Container App
// =====================================================
var sqlConnectionString = 'Server=tcp:${sqlServer.properties.fullyQualifiedDomainName},1433;Initial Catalog=${sqlDatabase.name};Persist Security Info=False;User ID=${sqlAdminLogin};Password=${sqlAdminPassword};MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;'

resource containerApp 'Microsoft.App/containerApps@2024-03-01' = {
  name: '${appName}-api'
  location: location
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    managedEnvironmentId: containerAppEnv.id
    configuration: {
      activeRevisionsMode: 'Single'
      ingress: {
        external: true
        targetPort: 8080
        transport: 'auto'
        allowInsecure: false
        traffic: [
          {
            latestRevision: true
            weight: 100
          }
        ]
      }
      registries: [
        {
          server: acrServer
          identity: 'system'
        }
      ]
      secrets: [
        {
          name: 'jwt-key'
          value: jwtKey
        }
        {
          name: 'api-key'
          value: apiKey
        }
        {
          name: 'sql-connection-string'
          value: sqlConnectionString
        }
      ]
    }
    template: {
      containers: [
        {
          name: '${appName}-api'
          image: '${acrServer}/${containerImage}:${containerImageTag}'
          resources: {
            cpu: json('0.25')
            memory: '0.5Gi'
          }
          env: [
            {
              name: 'ASPNETCORE_ENVIRONMENT'
              value: 'Production'
            }
            {
              name: 'ASPNETCORE_URLS'
              value: 'http://+:8080'
            }
            {
              name: 'ConnectionStrings__DefaultConnection'
              secretRef: 'sql-connection-string'
            }
            {
              name: 'Jwt__Key'
              secretRef: 'jwt-key'
            }
            {
              name: 'ApiKeys__0'
              secretRef: 'api-key'
            }
          ]
          probes: [
            {
              type: 'Liveness'
              httpGet: {
                path: '/alive'
                port: 8080
                scheme: 'HTTP'
              }
              initialDelaySeconds: 30
              periodSeconds: 30
              failureThreshold: 3
            }
            {
              type: 'Readiness'
              httpGet: {
                path: '/health'
                port: 8080
                scheme: 'HTTP'
              }
              initialDelaySeconds: 10
              periodSeconds: 10
              failureThreshold: 3
            }
          ]
        }
      ]
      scale: {
        minReplicas: 1
        maxReplicas: 2
      }
    }
    workloadProfileName: 'Consumption'
  }
}

// =====================================================
// Outputs
// =====================================================
output containerAppFqdn string = containerApp.properties.configuration.ingress.fqdn
output containerAppUrl string = 'https://${containerApp.properties.configuration.ingress.fqdn}'
output sqlServerFqdn string = sqlServer.properties.fullyQualifiedDomainName
output sqlDatabaseName string = sqlDatabase.name
