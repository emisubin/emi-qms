targetScope = 'resourceGroup'

@description('Backend managed identity resource ID from foundation.bicep.')
param backendIdentityId string

@description('Frontend managed identity resource ID from foundation.bicep.')
param frontendIdentityId string

@description('Migration managed identity resource ID from foundation.bicep.')
param migrationIdentityId string

@description('Database bootstrap managed identity resource ID from foundation.bicep.')
param databaseBootstrapIdentityId string

@description('Key Vault name from foundation.bicep. All required secrets must exist before this deployment.')
param keyVaultName string

@description('Optional existing role assignment name used to adopt a manually created Frontend Entra access-gate secret assignment during redeployment.')
param frontendAccessGateRoleAssignmentName string = ''

@description('Optional existing role assignment name used to adopt a manually created Backend Web Push VAPID public-key secret assignment during redeployment.')
param backendWebPushVapidPublicKeyRoleAssignmentName string = ''

@description('Optional existing role assignment name used to adopt a manually created Backend Web Push VAPID private-key secret assignment during redeployment.')
param backendWebPushVapidPrivateKeyRoleAssignmentName string = ''

var keyVaultSecretsUserRoleId = subscriptionResourceId(
  'Microsoft.Authorization/roleDefinitions',
  '4633458b-17de-408a-b874-0445c86b69e6'
)

resource backendIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' existing = {
  name: last(split(backendIdentityId, '/'))
}

resource frontendIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' existing = {
  name: last(split(frontendIdentityId, '/'))
}

resource migrationIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' existing = {
  name: last(split(migrationIdentityId, '/'))
}

resource databaseBootstrapIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' existing = {
  name: last(split(databaseBootstrapIdentityId, '/'))
}

resource keyVault 'Microsoft.KeyVault/vaults@2023-07-01' existing = {
  name: keyVaultName
}

resource databaseAdminSecret 'Microsoft.KeyVault/vaults/secrets@2023-07-01' existing = {
  parent: keyVault
  name: 'database-admin-connection-string'
}

resource databaseMigrationSecret 'Microsoft.KeyVault/vaults/secrets@2023-07-01' existing = {
  parent: keyVault
  name: 'database-migration-connection-string'
}

resource databaseRuntimeSecret 'Microsoft.KeyVault/vaults/secrets@2023-07-01' existing = {
  parent: keyVault
  name: 'database-runtime-connection-string'
}

resource bootstrapAdministratorsSecret 'Microsoft.KeyVault/vaults/secrets@2023-07-01' existing = {
  parent: keyVault
  name: 'bootstrap-administrator-emails'
}

resource developmentOperatorsSecret 'Microsoft.KeyVault/vaults/secrets@2023-07-01' existing = {
  parent: keyVault
  name: 'development-operator-emails'
}

resource originVerificationSecret 'Microsoft.KeyVault/vaults/secrets@2023-07-01' existing = {
  parent: keyVault
  name: 'front-door-origin-verify-token'
}

resource entraAccessGateSecret 'Microsoft.KeyVault/vaults/secrets@2023-07-01' existing = {
  parent: keyVault
  name: 'entra-access-gate-client-secret'
}

resource gmailUsernameSecret 'Microsoft.KeyVault/vaults/secrets@2023-07-01' existing = {
  parent: keyVault
  name: 'gmail-username'
}

resource gmailPasswordSecret 'Microsoft.KeyVault/vaults/secrets@2023-07-01' existing = {
  parent: keyVault
  name: 'gmail-app-password'
}

resource teamsActivitySecret 'Microsoft.KeyVault/vaults/secrets@2023-07-01' existing = {
  parent: keyVault
  name: 'teams-activity-client-secret'
}

resource webPushVapidPublicKeySecret 'Microsoft.KeyVault/vaults/secrets@2023-07-01' existing = {
  parent: keyVault
  name: 'web-push-vapid-public-key'
}

resource webPushVapidPrivateKeySecret 'Microsoft.KeyVault/vaults/secrets@2023-07-01' existing = {
  parent: keyVault
  name: 'web-push-vapid-private-key'
}

resource backendDatabaseSecretRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(databaseRuntimeSecret.id, backendIdentity.id, 'KeyVaultSecretsUser')
  scope: databaseRuntimeSecret
  properties: {
    principalId: backendIdentity.properties.principalId
    principalType: 'ServicePrincipal'
    roleDefinitionId: keyVaultSecretsUserRoleId
  }
}

resource backendAdministratorsSecretRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(bootstrapAdministratorsSecret.id, backendIdentity.id, 'KeyVaultSecretsUser')
  scope: bootstrapAdministratorsSecret
  properties: {
    principalId: backendIdentity.properties.principalId
    principalType: 'ServicePrincipal'
    roleDefinitionId: keyVaultSecretsUserRoleId
  }
}

resource backendDevelopmentOperatorsSecretRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(developmentOperatorsSecret.id, backendIdentity.id, 'KeyVaultSecretsUser')
  scope: developmentOperatorsSecret
  properties: {
    principalId: backendIdentity.properties.principalId
    principalType: 'ServicePrincipal'
    roleDefinitionId: keyVaultSecretsUserRoleId
  }
}

resource backendGmailUsernameSecretRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(gmailUsernameSecret.id, backendIdentity.id, 'KeyVaultSecretsUser')
  scope: gmailUsernameSecret
  properties: {
    principalId: backendIdentity.properties.principalId
    principalType: 'ServicePrincipal'
    roleDefinitionId: keyVaultSecretsUserRoleId
  }
}

resource backendGmailPasswordSecretRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(gmailPasswordSecret.id, backendIdentity.id, 'KeyVaultSecretsUser')
  scope: gmailPasswordSecret
  properties: {
    principalId: backendIdentity.properties.principalId
    principalType: 'ServicePrincipal'
    roleDefinitionId: keyVaultSecretsUserRoleId
  }
}

resource backendTeamsSecretRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(teamsActivitySecret.id, backendIdentity.id, 'KeyVaultSecretsUser')
  scope: teamsActivitySecret
  properties: {
    principalId: backendIdentity.properties.principalId
    principalType: 'ServicePrincipal'
    roleDefinitionId: keyVaultSecretsUserRoleId
  }
}

resource backendWebPushVapidPublicKeySecretRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: empty(backendWebPushVapidPublicKeyRoleAssignmentName)
    ? guid(webPushVapidPublicKeySecret.id, backendIdentity.id, 'KeyVaultSecretsUser')
    : backendWebPushVapidPublicKeyRoleAssignmentName
  scope: webPushVapidPublicKeySecret
  properties: {
    principalId: backendIdentity.properties.principalId
    principalType: 'ServicePrincipal'
    roleDefinitionId: keyVaultSecretsUserRoleId
  }
}

resource backendWebPushVapidPrivateKeySecretRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: empty(backendWebPushVapidPrivateKeyRoleAssignmentName)
    ? guid(webPushVapidPrivateKeySecret.id, backendIdentity.id, 'KeyVaultSecretsUser')
    : backendWebPushVapidPrivateKeyRoleAssignmentName
  scope: webPushVapidPrivateKeySecret
  properties: {
    principalId: backendIdentity.properties.principalId
    principalType: 'ServicePrincipal'
    roleDefinitionId: keyVaultSecretsUserRoleId
  }
}

resource frontendOriginSecretRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(originVerificationSecret.id, frontendIdentity.id, 'KeyVaultSecretsUser')
  scope: originVerificationSecret
  properties: {
    principalId: frontendIdentity.properties.principalId
    principalType: 'ServicePrincipal'
    roleDefinitionId: keyVaultSecretsUserRoleId
  }
}

resource frontendAccessGateSecretRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: empty(frontendAccessGateRoleAssignmentName)
    ? guid(entraAccessGateSecret.id, frontendIdentity.id, 'KeyVaultSecretsUser')
    : frontendAccessGateRoleAssignmentName
  scope: entraAccessGateSecret
  properties: {
    principalId: frontendIdentity.properties.principalId
    principalType: 'ServicePrincipal'
    roleDefinitionId: keyVaultSecretsUserRoleId
  }
}

resource migrationDatabaseSecretRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(databaseMigrationSecret.id, migrationIdentity.id, 'KeyVaultSecretsUser')
  scope: databaseMigrationSecret
  properties: {
    principalId: migrationIdentity.properties.principalId
    principalType: 'ServicePrincipal'
    roleDefinitionId: keyVaultSecretsUserRoleId
  }
}

resource bootstrapAdminDatabaseSecretRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(databaseAdminSecret.id, databaseBootstrapIdentity.id, 'KeyVaultSecretsUser')
  scope: databaseAdminSecret
  properties: {
    principalId: databaseBootstrapIdentity.properties.principalId
    principalType: 'ServicePrincipal'
    roleDefinitionId: keyVaultSecretsUserRoleId
  }
}

resource bootstrapMigrationDatabaseSecretRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(databaseMigrationSecret.id, databaseBootstrapIdentity.id, 'KeyVaultSecretsUser')
  scope: databaseMigrationSecret
  properties: {
    principalId: databaseBootstrapIdentity.properties.principalId
    principalType: 'ServicePrincipal'
    roleDefinitionId: keyVaultSecretsUserRoleId
  }
}

resource bootstrapRuntimeDatabaseSecretRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(databaseRuntimeSecret.id, databaseBootstrapIdentity.id, 'KeyVaultSecretsUser')
  scope: databaseRuntimeSecret
  properties: {
    principalId: databaseBootstrapIdentity.properties.principalId
    principalType: 'ServicePrincipal'
    roleDefinitionId: keyVaultSecretsUserRoleId
  }
}

output secretScopedRoleAssignmentCount int = 13
