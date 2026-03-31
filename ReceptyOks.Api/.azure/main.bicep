// =====================================================
// ReceptyOks API - Azure Infrastructure (Bicep)
// =====================================================
// Ten plik tworzy całą infrastrukturę dla backendu:
// - Container App Environment
// - Container App (receptyoks-api)
// - Storage Account + File Share (dla SQLite)
// - Container Registry (opcjonalnie)
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

// =====================================================
// Storage Account + File Share (dla SQLite)
// =====================================================
resource storageAccount 'Microsoft.Storage/storageAccounts@2023-01-01' = {
  name: '${appName}storage'
  location: location
  sku: {
    name: 'Standard_LRS'
  }
  kind: 'StorageV2'
  properties: {
    accessTier: 'Hot'
    minimumTlsVersion: 'TLS1_2'
    supportsHttpsTrafficOnly: true
  }
}

resource fileService 'Microsoft.Storage/storageAccounts/fileServices@2023-01-01' = {
  parent: storageAccount
  name: 'default'
}

resource fileShare 'Microsoft.Storage/storageAccounts/fileServices/shares@2023-01-01' = {
  parent: fileService
  name: '${appName}fileshare'
  properties: {
    shareQuota: 1 // 1 GB - wystarczy dla SQLite
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
      destination: 'azure-monitor' // Podstawowe logi bez dodatkowych kosztów
    }
  }
}

// Storage mount w Environment
resource envStorage 'Microsoft.App/managedEnvironments/storages@2024-03-01' = {
  parent: containerAppEnv
  name: '${appName}data'
  properties: {
    azureFile: {
      accountName: storageAccount.name
      accountKey: storageAccount.listKeys().keys[0].value
      shareName: fileShare.name
      accessMode: 'ReadWrite'
    }
  }
}

// =====================================================
// Container App
// =====================================================
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
              name: 'Database__DataFolder'
              value: '/data'
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
          volumeMounts: [
            {
              volumeName: 'data-volume'
              mountPath: '/data'
            }
          ]
        }
      ]
      scale: {
        minReplicas: 1
        maxReplicas: 1
      }
      volumes: [
        {
          name: 'data-volume'
          storageName: envStorage.name
          storageType: 'AzureFile'
        }
      ]
    }
    workloadProfileName: 'Consumption'
  }
}

// =====================================================
// Outputs
// =====================================================
output containerAppFqdn string = containerApp.properties.configuration.ingress.fqdn
output containerAppUrl string = 'https://${containerApp.properties.configuration.ingress.fqdn}'
output storageAccountName string = storageAccount.name
output fileShareName string = fileShare.name
