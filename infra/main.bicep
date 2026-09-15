targetScope = 'resourceGroup'

// StandFast runs as a single container app: Blazor Server in Azure Container Apps, with Azure Table Storage for data and Entra ID for sign-in.
// Every resource is named <AppBase><Env><Region><Suffix> and carries the standard tag set.

@description('Application base name, lower case. Forms the first segment of every resource name.')
param p_AppBase string = 'standfast'

@description('Environment code, for example PRD or DEV.')
@allowed([ 'PRD', 'DMO', 'UAT', 'DEV' ])
param p_Environment string = 'PRD'

@description('Region token used in resource names, for example usnorth.')
param p_RegionToken string = 'usnorth'

@description('Azure region the resources are created in.')
param p_Location string = resourceGroup().location

@description('Resource tags applied to all resources in this deployment.')
param p_Tags object

@description('Fully qualified container image, for example standfastprdusnorthcr.azurecr.io/standfast-ui:1.0.0.')
param p_ContainerImage string

@description('Entra ID tenant (directory) id the app signs users in against.')
param p_EntraTenantId string = subscription().tenantId

@description('Entra ID application (client) id of the StandFast app registration.')
param p_EntraClientId string

@description('Client secret for the app registration. Stored in Key Vault and surfaced to the container app as a Key Vault backed secret.')
@secure()
param p_EntraClientSecret string

@description('Windows or IANA time zone the board treats as today, for example Central Standard Time.')
param p_DisplayTimeZoneId string = 'Central Standard Time'

@description('Replica bounds. The floor stays at one so a scale to zero never drops a live standup circuit.')
param p_MinReplicas int = 1
param p_MaxReplicas int = 3

var v_NameBase = toLower('${p_AppBase}${p_Environment}${p_RegionToken}')
var v_ContainerAppName = '${v_NameBase}ca'
var v_ContainerPort = 8080
var v_HealthPath = '/healthz'
var v_DataProtectionContainer = 'dataprotection'
var v_DataProtectionBlob = 'keys.xml'
var v_ClientSecretName = 'azuread-client-secret'

// Built-in role definition ids. The container app holds one user assigned identity and is granted only the data-plane roles it needs.
var v_Roles = {
  storageTableDataContributor: '0a9a7e1f-b9d0-4cc4-a60d-0319b160aaa3'
  storageBlobDataContributor: 'ba92f5b4-2d11-453d-a403-e96b0029c9fe'
  keyVaultSecretsUser: '4633458b-17de-408a-b874-0445c86b69e6'
  acrPull: '7f951dda-4ed3-4680-a7ca-43fe172d538d'
}

resource rgTags 'Microsoft.Resources/tags@2021-04-01' = {
  name: 'default'
  properties: {
    tags: p_Tags
  }
}

resource identity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
  name: '${v_NameBase}mi'
  location: p_Location
  tags: p_Tags
}

resource workspace 'Microsoft.OperationalInsights/workspaces@2023-09-01' = {
  name: '${v_NameBase}oiw'
  location: p_Location
  tags: p_Tags
  properties: {
    sku: {
      name: 'PerGB2018'
    }
    retentionInDays: 30
  }
}

resource registry 'Microsoft.ContainerRegistry/registries@2023-11-01-preview' = {
  name: '${v_NameBase}cr'
  location: p_Location
  tags: p_Tags
  sku: {
    name: 'Basic'
  }
  properties: {
    adminUserEnabled: false
  }
}

resource storage 'Microsoft.Storage/storageAccounts@2023-05-01' = {
  name: '${v_NameBase}st'
  location: p_Location
  tags: p_Tags
  sku: {
    name: 'Standard_LRS'
  }
  kind: 'StorageV2'
  properties: {
    minimumTlsVersion: 'TLS1_2'
    supportsHttpsTrafficOnly: true
    allowBlobPublicAccess: false
    // Access is exclusively through the managed identity, so the account keys are switched off entirely rather than left unused.
    allowSharedKeyAccess: false
  }
}

resource blobService 'Microsoft.Storage/storageAccounts/blobServices@2023-05-01' = {
  parent: storage
  name: 'default'
}

// Blazor Server encrypts circuit and antiforgery state with the Data Protection key ring; replicas must share it, so it lives in blob storage rather than on each container's disk.
resource dataProtectionContainer 'Microsoft.Storage/storageAccounts/blobServices/containers@2023-05-01' = {
  parent: blobService
  name: v_DataProtectionContainer
  properties: {
    publicAccess: 'None'
  }
}

resource vault 'Microsoft.KeyVault/vaults@2023-07-01' = {
  name: '${v_NameBase}kv'
  location: p_Location
  tags: p_Tags
  properties: {
    sku: {
      family: 'A'
      name: 'standard'
    }
    tenantId: subscription().tenantId
    enableRbacAuthorization: true
    enableSoftDelete: true
    softDeleteRetentionInDays: 7
  }
}

resource clientSecret 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  parent: vault
  name: v_ClientSecretName
  properties: {
    value: p_EntraClientSecret
  }
}

resource tableRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  scope: storage
  name: guid(storage.id, identity.id, v_Roles.storageTableDataContributor)
  properties: {
    principalId: identity.properties.principalId
    principalType: 'ServicePrincipal'
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', v_Roles.storageTableDataContributor)
  }
}

resource blobRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  scope: storage
  name: guid(storage.id, identity.id, v_Roles.storageBlobDataContributor)
  properties: {
    principalId: identity.properties.principalId
    principalType: 'ServicePrincipal'
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', v_Roles.storageBlobDataContributor)
  }
}

resource vaultRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  scope: vault
  name: guid(vault.id, identity.id, v_Roles.keyVaultSecretsUser)
  properties: {
    principalId: identity.properties.principalId
    principalType: 'ServicePrincipal'
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', v_Roles.keyVaultSecretsUser)
  }
}

resource registryRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  scope: registry
  name: guid(registry.id, identity.id, v_Roles.acrPull)
  properties: {
    principalId: identity.properties.principalId
    principalType: 'ServicePrincipal'
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', v_Roles.acrPull)
  }
}

resource environment 'Microsoft.App/managedEnvironments@2024-03-01' = {
  name: '${v_NameBase}cae'
  location: p_Location
  tags: p_Tags
  properties: {
    appLogsConfiguration: {
      destination: 'log-analytics'
      logAnalyticsConfiguration: {
        customerId: workspace.properties.customerId
        sharedKey: workspace.listKeys().primarySharedKey
      }
    }
  }
}

resource containerApp 'Microsoft.App/containerApps@2024-03-01' = {
  name: v_ContainerAppName
  location: p_Location
  tags: p_Tags
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${identity.id}': {}
    }
  }
  properties: {
    managedEnvironmentId: environment.id
    configuration: {
      activeRevisionsMode: 'Single'
      ingress: {
        external: true
        targetPort: v_ContainerPort
        transport: 'auto'
        allowInsecure: false
        // Blazor Server holds a stateful SignalR circuit per browser. Without sticky sessions a reconnect can land on another replica and drop the circuit.
        stickySessions: {
          affinity: 'sticky'
        }
      }
      registries: [
        {
          server: registry.properties.loginServer
          identity: identity.id
        }
      ]
      secrets: [
        {
          name: v_ClientSecretName
          keyVaultUrl: clientSecret.properties.secretUri
          identity: identity.id
        }
      ]
    }
    template: {
      containers: [
        {
          name: v_ContainerAppName
          image: p_ContainerImage
          resources: {
            cpu: json('0.5')
            memory: '1Gi'
          }
          env: [
            { name: 'ASPNETCORE_ENVIRONMENT', value: 'Production' }
            // DefaultAzureCredential resolves the user assigned identity from this, so storage and Key Vault need no secret of their own.
            { name: 'AZURE_CLIENT_ID', value: identity.properties.clientId }
            { name: 'AzureAd__TenantId', value: p_EntraTenantId }
            { name: 'AzureAd__ClientId', value: p_EntraClientId }
            { name: 'AzureAd__ClientSecret', secretRef: v_ClientSecretName }
            { name: 'AzureTableStorage__ServiceUri', value: storage.properties.primaryEndpoints.table }
            { name: 'AzureTableStorage__TablePrefix', value: '${p_AppBase}${p_Environment}' }
            { name: 'StandFastUi__DisplayTimeZoneId', value: p_DisplayTimeZoneId }
            { name: 'StandFastUi__DataProtectionBlobUri', value: '${storage.properties.primaryEndpoints.blob}${v_DataProtectionContainer}/${v_DataProtectionBlob}' }
          ]
          probes: [
            {
              type: 'Liveness'
              httpGet: {
                path: v_HealthPath
                port: v_ContainerPort
              }
              periodSeconds: 30
            }
            {
              type: 'Readiness'
              httpGet: {
                path: v_HealthPath
                port: v_ContainerPort
              }
              periodSeconds: 10
            }
          ]
        }
      ]
      scale: {
        minReplicas: p_MinReplicas
        maxReplicas: p_MaxReplicas
      }
    }
  }
  dependsOn: [
    registryRole
    vaultRole
    tableRole
    blobRole
    dataProtectionContainer
  ]
}

@description('Public URL of the deployed app. Add https://<this>/signin-oidc as a redirect URI on the Entra ID app registration.')
output o_ApplicationUrl string = 'https://${containerApp.properties.configuration.ingress.fqdn}'

@description('Login server of the container registry the pipeline pushes to.')
output o_RegistryLoginServer string = registry.properties.loginServer

@description('Object id of the app identity, for granting any additional data-plane roles.')
output o_IdentityPrincipalId string = identity.properties.principalId
