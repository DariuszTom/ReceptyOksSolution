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

@description('Docker image name (without tag, including owner). Example: dariusztom/receptyoks-api')
param containerImage string

@description('Docker image tag. The Container App is pinned to a stable tag (default: release) that is promoted manually via the GitHub Actions workflow_dispatch on docker-build.yml. Untested images tagged :<sha> or :master are never pulled by the app.')
param containerImageTag string = 'release'

@description('Container registry host (e.g., ghcr.io). For public GHCR images no credentials are required.')
param containerRegistry string = 'ghcr.io'

// =====================================================
// SECRETS - these are stored in Azure Key Vault
// =====================================================
// The application reads secrets from Key Vault at startup via DefaultAzureCredential
// (see ReceptyOks.Api/Middleware/SecretsResolver.cs).
// Locally: developer credentials (az login) work.
// In Azure: Container App's Managed Identity has "Key Vault Secrets User" role.

@description('SQL administrator password')
@secure()
param sqlAdminPassword string

@description('SQL administrator login')
param sqlAdminLogin string = 'receptyoksadmin'

@description('Jwt:Key - JWT signing key (REQUIRED, min 32 chars). Read by Program.cs at startup.')
@secure()
@minLength(32)
param jwtKey string

@description('PasswordHash - Base64/hex-encoded password hash for API auth (REQUIRED by SecretsResolver). Read as Configuration["PasswordHash"].')
@secure()
param passwordHash string

@description('SecretKey - Base64/hex-encoded HMAC key for JWT auth. Read as Configuration["SecretKey"].')
@secure()
param secretKey string

@description('UserAgent - allowed username for token issuance. Read as Configuration["UserAgent"].')
param userAgent string = 'ReceptyOksApp'

@description('Token - Anthropic API token exposed via /token endpoint. Read as Configuration["Token"]. Leave empty if not using Anthropic.')
@secure()
param anthropicToken string = ''

// =====================================================
// Azure SQL Database
// =====================================================
@description('SQL Server name (must be globally unique). Defaults include a resource-group suffix to avoid collisions.')
param sqlServerName string = '${appName}-sql-${take(uniqueString(resourceGroup().id), 6)}'

resource sqlServer 'Microsoft.Sql/servers@2023-05-01-preview' = {
  name: sqlServerName
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
// Azure Key Vault
// =====================================================
// Stores application secrets (JWT key, API key, SQL connection string).
// Container App accesses secrets via System-Assigned Managed Identity + RBAC.
// Free tier: ~$0.03 / 10k operations - negligible cost.
@description('Key Vault name (must be globally unique, 3-24 chars, alphanumeric + hyphens). The default is truncated to fit the 24-char limit.')
param keyVaultName string = take('${appName}-kv-${uniqueString(resourceGroup().id)}', 24)

resource keyVault 'Microsoft.KeyVault/vaults@2023-07-01' = {
  name: keyVaultName
  location: location
  properties: {
    sku: {
      family: 'A'
      name: 'standard'
    }
    tenantId: subscription().tenantId
    enableRbacAuthorization: true
    enableSoftDelete: true
    softDeleteRetentionInDays: 7
    publicNetworkAccess: 'Enabled'
  }
}

// =====================================================
// Key Vault Secrets
// =====================================================
// Names MUST match what the application reads via IConfiguration:
//   Configuration["Jwt:Key"]      -> KV secret "Jwt--Key"     (Program.cs)
//   Configuration["PasswordHash"] -> KV secret "PasswordHash" (SecretsResolver, AuthEndpoints)
//   Configuration["SecretKey"]    -> KV secret "SecretKey"    (AuthEndpoints, TokenProviderEndpoints)
//   Configuration["UserAgent"]    -> KV secret "UserAgent"    (TokenProviderEndpoints)
//   Configuration["Token"]        -> KV secret "Token"        (TokenProviderEndpoints - Anthropic)
// Key Vault flattens '--' in secret names to ':' in IConfiguration.

// JWT signing key - REQUIRED by Program.cs (min 32 chars, else app crashes)
resource kvSecretJwtKey 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  parent: keyVault
  name: 'Jwt--Key'
  properties: {
    value: jwtKey
  }
}

// Password hash - REQUIRED by SecretsResolver if KV is configured (else app crashes)
resource kvSecretPasswordHash 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  parent: keyVault
  name: 'PasswordHash'
  properties: {
    value: passwordHash
  }
}

// HMAC key for JWT auth
resource kvSecretSecretKey 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  parent: keyVault
  name: 'SecretKey'
  properties: {
    value: secretKey
  }
}

// Allowed username for token issuance
resource kvSecretUserAgent 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  parent: keyVault
  name: 'UserAgent'
  properties: {
    value: userAgent
  }
}

// Anthropic API token (optional - conditional creation)
resource kvSecretToken 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = if (!empty(anthropicToken)) {
  parent: keyVault
  name: 'Token'
  properties: {
    value: anthropicToken
  }
}

// SQL connection string stored in KV for reference/other apps
// (Container App gets it inline via env var to avoid circular dependency)
resource kvSecretSqlConnection 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  parent: keyVault
  name: 'ConnectionStrings--DefaultConnection'
  properties: {
    value: sqlConnectionString
  }
}

// SQL admin password (for administrative access to database)
resource kvSecretSqlPassword 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  parent: keyVault
  name: 'sql-admin-password'
  properties: {
    value: sqlAdminPassword
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
      // No 'registries' entry: image is pulled anonymously from a public container registry
      // (default: GHCR, but 'containerRegistry' parameter allows any host).
      // If the image is switched to private (e.g., private GHCR), add:
      //   registries: [{ server: containerRegistry, username: '<user>', passwordSecretRef: 'registry-pull-token' }]
      // and store the credential as a Container App secret (e.g., 'registry-pull-token').
      // Only SQL connection string is stored inline as a Container App secret,
      // because it depends on runtime values (server FQDN) and is not read via Key Vault.
      // All application secrets (PasswordHash, SecretKey, UserAgent, etc.)
      // are read directly from Key Vault by the app using DefaultAzureCredential
      // (see ReceptyOks.Api/Middleware/SecretsResolver.cs).
      secrets: [
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
          image: '${containerRegistry}/${containerImage}:${containerImageTag}'
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
            // App reads all secrets (PasswordHash, SecretKey, UserAgent, etc.)
            // from Key Vault using DefaultAzureCredential + this URI.
            {
              name: 'KeyVault__VaultUri'
              value: keyVault.properties.vaultUri
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
        maxReplicas: 1
      }
    }
    // Note: 'workloadProfileName' is intentionally omitted.
    // The managed environment was created in "Consumption Only" mode (no workload profiles),
    // which rejects any workloadProfileName value - including 'Consumption'.
    // If the environment is later recreated with workload profiles enabled,
    // re-add: workloadProfileName: 'Consumption'
  }
}

// =====================================================
// RBAC: Container App -> Key Vault (Secrets User)
// =====================================================
// Grants Container App's System-Assigned MI read access to Key Vault secrets.
// This allows future migration to keyVaultUrl references without redeployment.
var keyVaultSecretsUserRoleId = '4633458b-17de-408a-b874-0445c86b69e6'

resource kvRoleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  scope: keyVault
  name: guid(keyVault.id, containerApp.id, keyVaultSecretsUserRoleId)
  properties: {
    principalId: containerApp.identity.principalId
    principalType: 'ServicePrincipal'
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', keyVaultSecretsUserRoleId)
  }
}

// =====================================================
// Outputs
// =====================================================
output containerAppFqdn string = containerApp.properties.configuration.ingress.fqdn
output containerAppUrl string = 'https://${containerApp.properties.configuration.ingress.fqdn}'
output sqlServerFqdn string = sqlServer.properties.fullyQualifiedDomainName
output sqlDatabaseName string = sqlDatabase.name
output keyVaultName string = keyVault.name
output keyVaultUri string = keyVault.properties.vaultUri
