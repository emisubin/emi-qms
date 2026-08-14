targetScope = 'resourceGroup'

@description('Short lowercase prefix used for Azure resource names.')
@minLength(3)
@maxLength(18)
param prefix string = 'pms-pilot'

@description('Azure region for the 20-day pilot.')
param location string = resourceGroup().location

@description('PostgreSQL administrator login. Do not use a personal identifier.')
param postgresAdministratorLogin string

@secure()
@description('PostgreSQL administrator password.')
param postgresAdministratorPassword string

@secure()
@description('Random value sent by Front Door and verified by the origin.')
param frontDoorOriginVerifyToken string

param virtualNetworkAddressPrefix string = '10.42.0.0/21'
param containerAppsSubnetPrefix string = '10.42.0.0/23'
param postgresSubnetPrefix string = '10.42.4.0/28'

var suffix = take(uniqueString(subscription().id, resourceGroup().id, prefix), 8)
var compactPrefix = replace(prefix, '-', '')
var names = {
  logWorkspace: '${prefix}-logs-${suffix}'
  appInsights: '${prefix}-insights-${suffix}'
  virtualNetwork: '${prefix}-vnet-${suffix}'
  privateDnsZone: 'privatelink.postgres.database.azure.com'
  environment: '${prefix}-env-${suffix}'
  registry: take('${compactPrefix}acr${suffix}', 50)
  storageAccount: take('${compactPrefix}st${suffix}', 24)
  keyVault: take('${prefix}-kv-${suffix}', 24)
  backendIdentity: '${prefix}-backend-${suffix}'
  frontendIdentity: '${prefix}-frontend-${suffix}'
  migrationIdentity: '${prefix}-migration-${suffix}'
  databaseBootstrapIdentity: '${prefix}-db-bootstrap-${suffix}'
  postgres: '${prefix}-pg-${suffix}'
  frontDoorProfile: '${prefix}-afd-${suffix}'
  frontDoorEndpoint: '${compactPrefix}-${suffix}'
  frontDoorRuleSet: 'originverification'
  wafPolicy: take('${compactPrefix}waf${suffix}', 128)
}

resource logWorkspace 'Microsoft.OperationalInsights/workspaces@2023-09-01' = {
  name: names.logWorkspace
  location: location
  properties: {
    retentionInDays: 30
    sku: {
      name: 'PerGB2018'
    }
    workspaceCapping: {
      dailyQuotaGb: 1
    }
  }
}

resource appInsights 'Microsoft.Insights/components@2020-02-02' = {
  name: names.appInsights
  location: location
  kind: 'web'
  properties: {
    Application_Type: 'web'
    WorkspaceResourceId: logWorkspace.id
  }
}

resource virtualNetwork 'Microsoft.Network/virtualNetworks@2024-05-01' = {
  name: names.virtualNetwork
  location: location
  properties: {
    addressSpace: {
      addressPrefixes: [
        virtualNetworkAddressPrefix
      ]
    }
    subnets: [
      {
        name: 'container-apps'
        properties: {
          addressPrefix: containerAppsSubnetPrefix
          delegations: [
            {
              name: 'container-apps-delegation'
              properties: {
                serviceName: 'Microsoft.App/environments'
              }
            }
          ]
        }
      }
      {
        name: 'postgres'
        properties: {
          addressPrefix: postgresSubnetPrefix
          delegations: [
            {
              name: 'postgres-delegation'
              properties: {
                serviceName: 'Microsoft.DBforPostgreSQL/flexibleServers'
              }
            }
          ]
          privateEndpointNetworkPolicies: 'Disabled'
        }
      }
    ]
  }
}

resource containerAppsSubnet 'Microsoft.Network/virtualNetworks/subnets@2024-05-01' existing = {
  parent: virtualNetwork
  name: 'container-apps'
}

resource postgresSubnet 'Microsoft.Network/virtualNetworks/subnets@2024-05-01' existing = {
  parent: virtualNetwork
  name: 'postgres'
}

resource privateDnsZone 'Microsoft.Network/privateDnsZones@2024-06-01' = {
  name: names.privateDnsZone
  location: 'global'
}

resource privateDnsLink 'Microsoft.Network/privateDnsZones/virtualNetworkLinks@2024-06-01' = {
  parent: privateDnsZone
  name: '${prefix}-postgres-link'
  location: 'global'
  properties: {
    registrationEnabled: false
    virtualNetwork: {
      id: virtualNetwork.id
    }
  }
}

resource containerAppsEnvironment 'Microsoft.App/managedEnvironments@2024-03-01' = {
  name: names.environment
  location: location
  properties: {
    appLogsConfiguration: {
      destination: 'log-analytics'
      logAnalyticsConfiguration: {
        customerId: logWorkspace.properties.customerId
        sharedKey: logWorkspace.listKeys().primarySharedKey
      }
    }
    zoneRedundant: false
    vnetConfiguration: {
      infrastructureSubnetId: containerAppsSubnet.id
      internal: false
    }
    workloadProfiles: [
      {
        name: 'Consumption'
        workloadProfileType: 'Consumption'
      }
    ]
  }
}

resource registry 'Microsoft.ContainerRegistry/registries@2023-11-01-preview' = {
  name: names.registry
  location: location
  sku: {
    name: 'Basic'
  }
  properties: {
    adminUserEnabled: false
    anonymousPullEnabled: false
    dataEndpointEnabled: false
    publicNetworkAccess: 'Enabled'
    zoneRedundancy: 'Disabled'
  }
}

resource storageAccount 'Microsoft.Storage/storageAccounts@2023-05-01' = {
  name: names.storageAccount
  location: location
  kind: 'StorageV2'
  sku: {
    name: 'Standard_LRS'
  }
  properties: {
    accessTier: 'Hot'
    allowBlobPublicAccess: false
    allowSharedKeyAccess: true
    defaultToOAuthAuthentication: false
    minimumTlsVersion: 'TLS1_2'
    publicNetworkAccess: 'Enabled'
    supportsHttpsTrafficOnly: true
  }
}

resource fileService 'Microsoft.Storage/storageAccounts/fileServices@2023-05-01' = {
  parent: storageAccount
  name: 'default'
}

resource clamAvShare 'Microsoft.Storage/storageAccounts/fileServices/shares@2023-05-01' = {
  parent: fileService
  name: 'clamav-signatures'
  properties: {
    accessTier: 'TransactionOptimized'
    enabledProtocols: 'SMB'
    shareQuota: 5
  }
}

resource environmentStorage 'Microsoft.App/managedEnvironments/storages@2024-03-01' = {
  parent: containerAppsEnvironment
  name: 'clamavsignatures'
  properties: {
    azureFile: {
      accessMode: 'ReadWrite'
      accountKey: storageAccount.listKeys().keys[0].value
      accountName: storageAccount.name
      shareName: clamAvShare.name
    }
  }
}

resource backendIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
  name: names.backendIdentity
  location: location
}

resource frontendIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
  name: names.frontendIdentity
  location: location
}

resource migrationIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
  name: names.migrationIdentity
  location: location
}

resource databaseBootstrapIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
  name: names.databaseBootstrapIdentity
  location: location
}

resource backendRegistryPullRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(registry.id, backendIdentity.id, 'AcrPull')
  scope: registry
  properties: {
    principalId: backendIdentity.properties.principalId
    principalType: 'ServicePrincipal'
    roleDefinitionId: subscriptionResourceId(
      'Microsoft.Authorization/roleDefinitions',
      '7f951dda-4ed3-4680-a7ca-43fe172d538d'
    )
  }
}

resource frontendRegistryPullRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(registry.id, frontendIdentity.id, 'AcrPull')
  scope: registry
  properties: {
    principalId: frontendIdentity.properties.principalId
    principalType: 'ServicePrincipal'
    roleDefinitionId: subscriptionResourceId(
      'Microsoft.Authorization/roleDefinitions',
      '7f951dda-4ed3-4680-a7ca-43fe172d538d'
    )
  }
}

resource migrationRegistryPullRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(registry.id, migrationIdentity.id, 'AcrPull')
  scope: registry
  properties: {
    principalId: migrationIdentity.properties.principalId
    principalType: 'ServicePrincipal'
    roleDefinitionId: subscriptionResourceId(
      'Microsoft.Authorization/roleDefinitions',
      '7f951dda-4ed3-4680-a7ca-43fe172d538d'
    )
  }
}

resource databaseBootstrapRegistryPullRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(registry.id, databaseBootstrapIdentity.id, 'AcrPull')
  scope: registry
  properties: {
    principalId: databaseBootstrapIdentity.properties.principalId
    principalType: 'ServicePrincipal'
    roleDefinitionId: subscriptionResourceId(
      'Microsoft.Authorization/roleDefinitions',
      '7f951dda-4ed3-4680-a7ca-43fe172d538d'
    )
  }
}

resource keyVault 'Microsoft.KeyVault/vaults@2023-07-01' = {
  name: names.keyVault
  location: location
  properties: {
    accessPolicies: []
    enablePurgeProtection: true
    enableRbacAuthorization: true
    enableSoftDelete: true
    publicNetworkAccess: 'Enabled'
    sku: {
      family: 'A'
      name: 'standard'
    }
    softDeleteRetentionInDays: 7
    tenantId: subscription().tenantId
  }
}

resource postgresServer 'Microsoft.DBforPostgreSQL/flexibleServers@2024-08-01' = {
  name: names.postgres
  location: location
  sku: {
    name: 'Standard_B2s'
    tier: 'Burstable'
  }
  properties: {
    administratorLogin: postgresAdministratorLogin
    administratorLoginPassword: postgresAdministratorPassword
    authConfig: {
      activeDirectoryAuth: 'Disabled'
      passwordAuth: 'Enabled'
    }
    availabilityZone: '1'
    backup: {
      backupRetentionDays: 14
      geoRedundantBackup: 'Disabled'
    }
    createMode: 'Create'
    highAvailability: {
      mode: 'Disabled'
    }
    network: {
      delegatedSubnetResourceId: postgresSubnet.id
      privateDnsZoneArmResourceId: privateDnsZone.id
      publicNetworkAccess: 'Disabled'
    }
    storage: {
      autoGrow: 'Enabled'
      storageSizeGB: 32
    }
    version: '16'
  }
  dependsOn: [
    privateDnsLink
  ]
}

resource qmsDatabase 'Microsoft.DBforPostgreSQL/flexibleServers/databases@2024-08-01' = {
  parent: postgresServer
  name: 'emi_qms'
  properties: {
    charset: 'UTF8'
    collation: 'en_US.utf8'
  }
}

resource frontDoorProfile 'Microsoft.Cdn/profiles@2024-09-01' = {
  name: names.frontDoorProfile
  location: 'global'
  sku: {
    name: 'Standard_AzureFrontDoor'
  }
}

resource frontDoorEndpoint 'Microsoft.Cdn/profiles/afdEndpoints@2024-09-01' = {
  parent: frontDoorProfile
  name: names.frontDoorEndpoint
  location: 'global'
  properties: {
    enabledState: 'Enabled'
  }
}

resource originVerificationRuleSet 'Microsoft.Cdn/profiles/ruleSets@2024-09-01' = {
  parent: frontDoorProfile
  name: names.frontDoorRuleSet
}

resource originVerificationRule 'Microsoft.Cdn/profiles/ruleSets/rules@2024-09-01' = {
  parent: originVerificationRuleSet
  name: 'addoriginverification'
  properties: {
    actions: [
      {
        name: 'ModifyRequestHeader'
        parameters: {
          headerAction: 'Overwrite'
          headerName: 'X-Pms-Origin-Verify'
          typeName: 'DeliveryRuleHeaderActionParameters'
          value: frontDoorOriginVerifyToken
        }
      }
    ]
    conditions: []
    matchProcessingBehavior: 'Continue'
    order: 1
  }
}

resource wafPolicy 'Microsoft.Network/frontDoorWebApplicationFirewallPolicies@2024-02-01' = {
  name: names.wafPolicy
  location: 'global'
  sku: {
    name: 'Standard_AzureFrontDoor'
  }
  properties: {
    customRules: {
      rules: [
        {
          action: 'Block'
          enabledState: 'Enabled'
          matchConditions: [
            {
              matchValue: [
                '192.0.2.0/24'
              ]
              matchVariable: 'RemoteAddr'
              negateCondition: true
              operator: 'IPMatch'
            }
          ]
          name: 'GlobalRateLimit'
          priority: 1
          rateLimitDurationInMinutes: 1
          rateLimitThreshold: 300
          ruleType: 'RateLimitRule'
        }
      ]
    }
    policySettings: {
      enabledState: 'Enabled'
      mode: 'Prevention'
      requestBodyCheck: 'Enabled'
    }
  }
}

resource keyVaultDiagnostics 'Microsoft.Insights/diagnosticSettings@2021-05-01-preview' = {
  name: 'send-to-pilot-workspace'
  scope: keyVault
  properties: {
    logs: [
      {
        categoryGroup: 'allLogs'
        enabled: true
      }
    ]
    metrics: [
      {
        category: 'AllMetrics'
        enabled: true
      }
    ]
    workspaceId: logWorkspace.id
  }
}

resource postgresDiagnostics 'Microsoft.Insights/diagnosticSettings@2021-05-01-preview' = {
  name: 'send-to-pilot-workspace'
  scope: postgresServer
  properties: {
    logs: [
      {
        categoryGroup: 'allLogs'
        enabled: true
      }
    ]
    metrics: [
      {
        category: 'AllMetrics'
        enabled: true
      }
    ]
    workspaceId: logWorkspace.id
  }
}

resource frontDoorDiagnostics 'Microsoft.Insights/diagnosticSettings@2021-05-01-preview' = {
  name: 'send-to-pilot-workspace'
  scope: frontDoorProfile
  properties: {
    logs: [
      {
        categoryGroup: 'allLogs'
        enabled: true
      }
    ]
    metrics: [
      {
        category: 'AllMetrics'
        enabled: true
      }
    ]
    workspaceId: logWorkspace.id
  }
}

output containerAppsEnvironmentName string = containerAppsEnvironment.name
output containerAppsEnvironmentId string = containerAppsEnvironment.id
output containerAppsSubnetPrefix string = containerAppsSubnetPrefix
output registryName string = registry.name
output registryServer string = registry.properties.loginServer
output backendIdentityId string = backendIdentity.id
output frontendIdentityId string = frontendIdentity.id
output migrationIdentityId string = migrationIdentity.id
output databaseBootstrapIdentityId string = databaseBootstrapIdentity.id
output keyVaultName string = keyVault.name
output keyVaultUri string = keyVault.properties.vaultUri
output postgresHost string = postgresServer.properties.fullyQualifiedDomainName
output postgresDatabaseName string = qmsDatabase.name
output applicationInsightsConnectionString string = appInsights.properties.ConnectionString
output frontDoorProfileName string = frontDoorProfile.name
output frontDoorEndpointName string = frontDoorEndpoint.name
output frontDoorEndpointHost string = frontDoorEndpoint.properties.hostName
output frontDoorId string = frontDoorProfile.properties.frontDoorId
output frontDoorRuleSetId string = originVerificationRuleSet.id
output wafPolicyId string = wafPolicy.id
output requiredKeyVaultSecretNames array = [
  'database-admin-connection-string'
  'database-migration-connection-string'
  'database-runtime-connection-string'
  'bootstrap-administrator-emails'
  'front-door-origin-verify-token'
  'entra-access-gate-client-secret'
  'gmail-username'
  'gmail-app-password'
  'teams-activity-client-secret'
  'web-push-vapid-public-key'
  'web-push-vapid-private-key'
]
