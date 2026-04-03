// =====================================================
// ReceptyOks API - Azure Infrastructure (Bicep)
// =====================================================
// This file creates the full infrastructure for the backend:
// - Azure SQL Database (Basic tier)
// - Container App Environment
// - Container App (receptyoks-api)
// - Key Vault (for secrets)
//
// Deployment:
//   az deployment group create \
//     --resource-group <RG> \
//     --template-file main.bicep \
//     --parameters main.bicepparam
// =====================================================

@description('Location for all resources')
param location string = resourceGroup().location

@description('Application name (used as a prefix)')
param appName string = 'receptyoks'

@description('Docker image name (without tag)')
param containerImage string

@description('Docker image tag')
param containerImageTag string = 'latest'

@description('ACR server name (e.g., myacr.azurecr.io)')
param acrServer string

@description('JWT key for API authentication')
@secure()
param jwtKey string

@description('API key for endpoint access')
@secure()
param apiKey string

@description('SQL administrator password')
@secure()
param sqlAdminPassword string

@description('SQL administrator login')
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
// Log Analytics Workspace (optional - for logs)
// =====================================================
@description('Create Log Analytics workspace? (false = cheaper option)')
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
