targetScope = 'resourceGroup'

@description('Azure region used by the foundation deployment.')
param location string = resourceGroup().location

@description('Container Apps managed environment name from foundation.bicep.')
param containerAppsEnvironmentName string

@description('Container Apps infrastructure subnet CIDR trusted by the API proxy middleware.')
param containerAppsSubnetPrefix string

@description('Container Registry login server from foundation.bicep.')
param registryServer string

@description('Backend user-assigned managed identity resource ID from foundation.bicep.')
param backendIdentityId string

@description('Frontend user-assigned managed identity resource ID from foundation.bicep.')
param frontendIdentityId string

@description('Migration user-assigned managed identity resource ID from foundation.bicep.')
param migrationIdentityId string

@description('Database bootstrap user-assigned managed identity resource ID from foundation.bicep.')
param databaseBootstrapIdentityId string

@description('Key Vault name from foundation.bicep.')
param keyVaultName string

@description('Application Insights connection string from foundation.bicep.')
param applicationInsightsConnectionString string

@description('Front Door profile identifier used to reject direct origin requests.')
param frontDoorId string

@description('Complete immutable Backend image reference in ACR.')
param backendImage string

@description('Complete immutable Azure Frontend image reference in ACR.')
param frontendImage string

@description('Immutable ClamAV image reference.')
param clamAvImage string = 'clamav/clamav:1.4@sha256:6b7c8e09559250f25b0184516b0a2ae805136e57485260e16c780c9fd6e6aba9'

@description('Exact public hostname. Pass at deployment time; do not commit the real value.')
param publicHost string

@description('Entra tenant identifier.')
param entraTenantId string

@description('Entra API application client identifier.')
param entraApiClientId string

@description('Entra SPA application client identifier.')
param entraSpaClientId string

@description('Single-tenant Entra web application client identifier used by the Frontend pre-authentication gate.')
param entraAccessGateClientId string

@description('Entra API audience.')
param entraApiAudience string

@description('Entra verified domain.')
param entraDomain string

@description('ISO-8601 time of the most recent successful managed database restore rehearsal.')
param restoreVerifiedAtUtc string

@description('Privacy-safe monitoring sink label, not an email address.')
param securityAlertSink string = 'azure-monitor-pilot'

@description('Teams activity application client identifier.')
param teamsActivityClientId string

@description('Teams catalog application identifier.')
param teamsCatalogAppId string

@description('Teams manifest external identifier.')
param teamsManifestExternalId string

@description('Enable actual Teams, Gmail, and Web Push delivery after provider smoke succeeds.')
param enableExternalNotifications bool = false

@description('Activate minimum replicas only after migration and readiness gates pass.')
param activateWorkloads bool = false

var enabled = enableExternalNotifications ? 'true' : 'false'
var disabled = enableExternalNotifications ? 'false' : 'true'
var minimumReplicaCount = activateWorkloads ? 1 : 0
var publicOrigin = 'https://${publicHost}'

resource containerAppsEnvironment 'Microsoft.App/managedEnvironments@2024-03-01' existing = {
  name: containerAppsEnvironmentName
}

var backendInternalHost = 'backend.internal.${containerAppsEnvironment.properties.defaultDomain}'

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

var keyVaultSecretBase = '${keyVault.properties.vaultUri}secrets/'
var backendRegistryConfiguration = [
  {
    identity: backendIdentity.id
    server: registryServer
  }
]
var frontendRegistryConfiguration = [
  {
    identity: frontendIdentity.id
    server: registryServer
  }
]
var migrationRegistryConfiguration = [
  {
    identity: migrationIdentity.id
    server: registryServer
  }
]
var databaseBootstrapRegistryConfiguration = [
  {
    identity: databaseBootstrapIdentity.id
    server: registryServer
  }
]
var backendSecrets = [
  {
    identity: backendIdentity.id
    keyVaultUrl: '${keyVaultSecretBase}database-runtime-connection-string'
    name: 'database-runtime-connection-string'
  }
  {
    identity: backendIdentity.id
    keyVaultUrl: '${keyVaultSecretBase}bootstrap-administrator-emails'
    name: 'bootstrap-administrator-emails'
  }
  {
    identity: backendIdentity.id
    keyVaultUrl: '${keyVaultSecretBase}development-operator-emails'
    name: 'development-operator-emails'
  }
  {
    identity: backendIdentity.id
    keyVaultUrl: '${keyVaultSecretBase}gmail-username'
    name: 'gmail-username'
  }
  {
    identity: backendIdentity.id
    keyVaultUrl: '${keyVaultSecretBase}gmail-app-password'
    name: 'gmail-app-password'
  }
  {
    identity: backendIdentity.id
    keyVaultUrl: '${keyVaultSecretBase}teams-activity-client-secret'
    name: 'teams-activity-client-secret'
  }
  {
    identity: backendIdentity.id
    keyVaultUrl: '${keyVaultSecretBase}web-push-vapid-public-key'
    name: 'web-push-vapid-public-key'
  }
  {
    identity: backendIdentity.id
    keyVaultUrl: '${keyVaultSecretBase}web-push-vapid-private-key'
    name: 'web-push-vapid-private-key'
  }
]
var servingBackendEnvironment = [
  {
    name: 'ASPNETCORE_ENVIRONMENT'
    value: 'Production'
  }
  {
    name: 'ASPNETCORE_URLS'
    value: 'http://+:8080'
  }
  {
    name: 'AllowedHosts'
    value: '${publicHost};${backendInternalHost}'
  }
  {
    name: 'Authentication__Mode'
    value: 'EntraId'
  }
  {
    name: 'Authentication__BootstrapAdminEmails'
    secretRef: 'bootstrap-administrator-emails'
  }
  {
    name: 'Authentication__DevelopmentOperatorEmails'
    secretRef: 'development-operator-emails'
  }
  {
    name: 'AzureAd__Instance'
    value: environment().authentication.loginEndpoint
  }
  {
    name: 'AzureAd__TenantId'
    value: entraTenantId
  }
  {
    name: 'AzureAd__ClientId'
    value: entraApiClientId
  }
  {
    name: 'AzureAd__SpaClientId'
    value: entraSpaClientId
  }
  {
    name: 'AzureAd__Audience'
    value: entraApiAudience
  }
  {
    name: 'AzureAd__Domain'
    value: entraDomain
  }
  {
    name: 'Frontend__Origin'
    value: publicOrigin
  }
  {
    name: 'Frontend__RedirectUri'
    value: publicOrigin
  }
  {
    name: 'ReverseProxy__KnownNetworks'
    value: containerAppsSubnetPrefix
  }
  {
    name: 'RateLimiting__Enabled'
    value: 'true'
  }
  {
    name: 'UploadSecurity__Enabled'
    value: 'true'
  }
  {
    name: 'UploadSecurity__FailClosed'
    value: 'true'
  }
  {
    name: 'UploadSecurity__ScannerHost'
    value: clamAv.properties.configuration.ingress.fqdn
  }
  {
    name: 'UploadSecurity__ScannerPort'
    value: '3310'
  }
  {
    name: 'UploadSecurity__RejectImageMetadata'
    value: 'true'
  }
  {
    name: 'Database__ApplyMigrationsOnStartup'
    value: 'false'
  }
  {
    name: 'Database__RuntimeRoleName'
    value: 'pms_app'
  }
  {
    name: 'DevelopmentData__SeedEnabled'
    value: 'false'
  }
  {
    name: 'DevAuthentication__Enabled'
    value: 'false'
  }
  {
    name: 'AdminUserSwitch__Enabled'
    value: 'false'
  }
  {
    name: 'Operations__Backup__RestoreVerifiedAtUtc'
    value: restoreVerifiedAtUtc
  }
  {
    name: 'Operations__Monitoring__Enabled'
    value: 'true'
  }
  {
    name: 'Operations__Monitoring__SecurityAlertSink'
    value: securityAlertSink
  }
  {
    name: 'ConnectionStrings__QmsDatabase'
    secretRef: 'database-runtime-connection-string'
  }
  {
    name: 'APPLICATIONINSIGHTS_CONNECTION_STRING'
    value: applicationInsightsConnectionString
  }
  {
    name: 'Notifications__Links__BaseUrl'
    value: publicOrigin
  }
  {
    name: 'Notifications__Dispatch__Enabled'
    value: enabled
  }
  {
    name: 'Notifications__TeamsActivity__Enabled'
    value: enabled
  }
  {
    name: 'Notifications__TeamsActivity__DryRun'
    value: disabled
  }
  {
    name: 'Notifications__TeamsActivity__PersonalChannelStrategy'
    value: 'TeamsActivity'
  }
  {
    name: 'Notifications__TeamsActivity__TenantId'
    value: entraTenantId
  }
  {
    name: 'Notifications__TeamsActivity__ClientId'
    value: teamsActivityClientId
  }
  {
    name: 'Notifications__TeamsActivity__ClientSecret'
    secretRef: 'teams-activity-client-secret'
  }
  {
    name: 'Notifications__TeamsActivity__ManifestId'
    value: teamsManifestExternalId
  }
  {
    name: 'Notifications__TeamsActivity__TeamsManifestExternalId'
    value: teamsManifestExternalId
  }
  {
    name: 'Notifications__TeamsActivity__TeamsAppId'
    value: teamsActivityClientId
  }
  {
    name: 'Notifications__TeamsActivity__TeamsCatalogAppId'
    value: teamsCatalogAppId
  }
  {
    name: 'Notifications__TeamsActivity__TopicWebUrl'
    value: publicOrigin
  }
  {
    name: 'Notifications__WebPush__Enabled'
    value: enabled
  }
  {
    name: 'Notifications__WebPush__DryRun'
    value: disabled
  }
  {
    name: 'Notifications__WebPush__PublicKey'
    secretRef: 'web-push-vapid-public-key'
  }
  {
    name: 'Notifications__WebPush__PrivateKey'
    secretRef: 'web-push-vapid-private-key'
  }
  {
    name: 'Notifications__Mail__Enabled'
    value: enabled
  }
  {
    name: 'Notifications__Mail__DryRun'
    value: disabled
  }
  {
    name: 'Notifications__Mail__Provider'
    value: 'Smtp'
  }
  {
    name: 'Notifications__Mail__SenderAddress'
    secretRef: 'gmail-username'
  }
  {
    name: 'Notifications__Mail__Smtp__Host'
    value: 'smtp.gmail.com'
  }
  {
    name: 'Notifications__Mail__Smtp__Port'
    value: '587'
  }
  {
    name: 'Notifications__Mail__Smtp__Security'
    value: 'StartTls'
  }
  {
    name: 'Notifications__Mail__Smtp__Username'
    secretRef: 'gmail-username'
  }
  {
    name: 'Notifications__Mail__Smtp__Password'
    secretRef: 'gmail-app-password'
  }
]

var migrationSecrets = [
  {
    identity: migrationIdentity.id
    keyVaultUrl: '${keyVaultSecretBase}database-migration-connection-string'
    name: 'database-migration-connection-string'
  }
]

var migrationEnvironment = [
  {
    name: 'ASPNETCORE_ENVIRONMENT'
    value: 'Production'
  }
  {
    name: 'ConnectionStrings__QmsDatabase'
    secretRef: 'database-migration-connection-string'
  }
  {
    name: 'Database__MigrationRoleName'
    value: 'pms_migrator'
  }
  {
    name: 'Database__RuntimeRoleName'
    value: 'pms_app'
  }
]

var databaseBootstrapSecrets = [
  {
    identity: databaseBootstrapIdentity.id
    keyVaultUrl: '${keyVaultSecretBase}database-admin-connection-string'
    name: 'database-admin-connection-string'
  }
  {
    identity: databaseBootstrapIdentity.id
    keyVaultUrl: '${keyVaultSecretBase}database-migration-connection-string'
    name: 'database-migration-connection-string'
  }
  {
    identity: databaseBootstrapIdentity.id
    keyVaultUrl: '${keyVaultSecretBase}database-runtime-connection-string'
    name: 'database-runtime-connection-string'
  }
]

var databaseBootstrapEnvironment = [
  {
    name: 'ASPNETCORE_ENVIRONMENT'
    value: 'Production'
  }
  {
    name: 'ConnectionStrings__QmsDatabaseAdmin'
    secretRef: 'database-admin-connection-string'
  }
  {
    name: 'ConnectionStrings__QmsDatabaseMigration'
    secretRef: 'database-migration-connection-string'
  }
  {
    name: 'ConnectionStrings__QmsDatabaseRuntime'
    secretRef: 'database-runtime-connection-string'
  }
]

resource clamAv 'Microsoft.App/containerApps@2024-03-01' = {
  name: 'clamav'
  location: location
  identity: {
    type: 'None'
  }
  properties: {
    environmentId: containerAppsEnvironment.id
    configuration: {
      activeRevisionsMode: 'Single'
      ingress: {
        allowInsecure: false
        exposedPort: 3310
        external: false
        targetPort: 3310
        traffic: [
          {
            latestRevision: true
            weight: 100
          }
        ]
        transport: 'tcp'
      }
    }
    template: {
      containers: [
        {
          image: clamAvImage
          name: 'clamav'
          probes: [
            {
              failureThreshold: 12
              initialDelaySeconds: 20
              periodSeconds: 10
              tcpSocket: {
                port: 3310
              }
              timeoutSeconds: 5
              type: 'Startup'
            }
            {
              failureThreshold: 3
              periodSeconds: 30
              tcpSocket: {
                port: 3310
              }
              timeoutSeconds: 5
              type: 'Liveness'
            }
          ]
          resources: {
            cpu: json('2.0')
            memory: '4Gi'
          }
          volumeMounts: [
            {
              mountPath: '/var/lib/clamav'
              volumeName: 'clamav-signatures'
            }
          ]
        }
      ]
      scale: {
        maxReplicas: 1
        minReplicas: minimumReplicaCount
      }
      volumes: [
        {
          name: 'clamav-signatures'
          storageName: 'clamavsignatures'
          storageType: 'AzureFile'
        }
      ]
    }
    workloadProfileName: 'Consumption'
  }
}

resource backend 'Microsoft.App/containerApps@2024-03-01' = {
  name: 'backend'
  location: location
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${backendIdentity.id}': {}
    }
  }
  properties: {
    environmentId: containerAppsEnvironment.id
    configuration: {
      activeRevisionsMode: 'Single'
      ingress: {
        allowInsecure: false
        external: false
        targetPort: 8080
        traffic: [
          {
            latestRevision: true
            weight: 100
          }
        ]
        transport: 'http'
      }
      registries: backendRegistryConfiguration
      secrets: backendSecrets
    }
    template: {
      containers: [
        {
          env: servingBackendEnvironment
          image: backendImage
          name: 'backend'
          probes: [
            {
              failureThreshold: 12
              httpGet: {
                httpHeaders: [
                  {
                    name: 'Host'
                    value: publicHost
                  }
                ]
                path: '/health/live'
                port: 8080
                scheme: 'HTTP'
              }
              initialDelaySeconds: 5
              periodSeconds: 5
              timeoutSeconds: 3
              type: 'Startup'
            }
            {
              failureThreshold: 3
              httpGet: {
                httpHeaders: [
                  {
                    name: 'Host'
                    value: publicHost
                  }
                ]
                path: '/health/live'
                port: 8080
                scheme: 'HTTP'
              }
              periodSeconds: 20
              timeoutSeconds: 3
              type: 'Liveness'
            }
            {
              failureThreshold: 3
              httpGet: {
                httpHeaders: [
                  {
                    name: 'Host'
                    value: publicHost
                  }
                ]
                path: '/health/ready'
                port: 8080
                scheme: 'HTTP'
              }
              periodSeconds: 10
              timeoutSeconds: 5
              type: 'Readiness'
            }
          ]
          resources: {
            cpu: json('1.0')
            memory: '2Gi'
          }
        }
      ]
      scale: {
        maxReplicas: 1
        minReplicas: minimumReplicaCount
      }
    }
    workloadProfileName: 'Consumption'
  }
}

resource migrationJob 'Microsoft.App/jobs@2024-03-01' = {
  name: 'migration'
  location: location
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${migrationIdentity.id}': {}
    }
  }
  properties: {
    environmentId: containerAppsEnvironment.id
    configuration: {
      manualTriggerConfig: {
        parallelism: 1
        replicaCompletionCount: 1
      }
      registries: migrationRegistryConfiguration
      replicaRetryLimit: 0
      replicaTimeout: 1800
      secrets: migrationSecrets
      triggerType: 'Manual'
    }
    template: {
      containers: [
        {
          args: [
            '--migrate-only'
          ]
          env: migrationEnvironment
          image: backendImage
          name: 'migration'
          resources: {
            cpu: json('1.0')
            memory: '2Gi'
          }
        }
      ]
    }
    workloadProfileName: 'Consumption'
  }
}

resource databaseBootstrapJob 'Microsoft.App/jobs@2024-03-01' = {
  name: 'database-role-bootstrap'
  location: location
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${databaseBootstrapIdentity.id}': {}
    }
  }
  properties: {
    environmentId: containerAppsEnvironment.id
    configuration: {
      manualTriggerConfig: {
        parallelism: 1
        replicaCompletionCount: 1
      }
      registries: databaseBootstrapRegistryConfiguration
      replicaRetryLimit: 0
      replicaTimeout: 900
      secrets: databaseBootstrapSecrets
      triggerType: 'Manual'
    }
    template: {
      containers: [
        {
          args: [
            '--bootstrap-database-roles'
          ]
          env: databaseBootstrapEnvironment
          image: backendImage
          name: 'database-role-bootstrap'
          resources: {
            cpu: json('0.5')
            memory: '1Gi'
          }
        }
      ]
    }
    workloadProfileName: 'Consumption'
  }
}

resource originVerifySecret 'Microsoft.KeyVault/vaults/secrets@2023-07-01' existing = {
  parent: keyVault
  name: 'front-door-origin-verify-token'
}

resource entraAccessGateSecret 'Microsoft.KeyVault/vaults/secrets@2023-07-01' existing = {
  parent: keyVault
  name: 'entra-access-gate-client-secret'
}

resource frontend 'Microsoft.App/containerApps@2024-03-01' = {
  name: 'frontend'
  location: location
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${frontendIdentity.id}': {}
    }
  }
  properties: {
    environmentId: containerAppsEnvironment.id
    configuration: {
      activeRevisionsMode: 'Single'
      ingress: {
        allowInsecure: false
        external: true
        targetPort: 8080
        traffic: [
          {
            latestRevision: true
            weight: 100
          }
        ]
        transport: 'http'
      }
      registries: frontendRegistryConfiguration
      secrets: [
        {
          identity: frontendIdentity.id
          keyVaultUrl: originVerifySecret.properties.secretUri
          name: 'origin-verify-token'
        }
        {
          identity: frontendIdentity.id
          keyVaultUrl: entraAccessGateSecret.properties.secretUri
          name: 'entra-access-gate-client-secret'
        }
      ]
    }
    template: {
      containers: [
        {
          env: [
            {
              name: 'PUBLIC_HOST'
              value: publicHost
            }
            {
              name: 'BACKEND_FQDN'
              value: backend.properties.configuration.ingress.fqdn
            }
            {
              name: 'FRONT_DOOR_ID'
              value: frontDoorId
            }
            {
              name: 'ORIGIN_VERIFY_TOKEN'
              secretRef: 'origin-verify-token'
            }
            {
              name: 'NGINX_ENVSUBST_FILTER'
              value: '^(PUBLIC_HOST|BACKEND_FQDN|FRONT_DOOR_ID|ORIGIN_VERIFY_TOKEN)$'
            }
          ]
          image: frontendImage
          name: 'frontend'
          probes: [
            {
              failureThreshold: 12
              httpGet: {
                path: '/health/live'
                port: 8080
                scheme: 'HTTP'
              }
              initialDelaySeconds: 2
              periodSeconds: 5
              timeoutSeconds: 3
              type: 'Startup'
            }
            {
              failureThreshold: 3
              httpGet: {
                path: '/health/live'
                port: 8080
                scheme: 'HTTP'
              }
              periodSeconds: 20
              timeoutSeconds: 3
              type: 'Liveness'
            }
          ]
          resources: {
            cpu: json('0.25')
            memory: '0.5Gi'
          }
        }
      ]
      scale: {
        maxReplicas: 2
        minReplicas: minimumReplicaCount
      }
    }
    workloadProfileName: 'Consumption'
  }
}

resource frontendAuth 'Microsoft.App/containerApps/authConfigs@2024-03-01' = {
  parent: frontend
  name: 'current'
  properties: {
    globalValidation: {
      excludedPaths: [
        '/health/live'
        '/teams-launcher.html'
        '/teams-launcher.js'
        '/icons/emi-qms-192.png'
      ]
      redirectToProvider: 'azureactivedirectory'
      unauthenticatedClientAction: 'RedirectToLoginPage'
    }
    httpSettings: {
      forwardProxy: {
        convention: 'Standard'
      }
      requireHttps: true
      routes: {
        apiPrefix: '/.auth'
      }
    }
    identityProviders: {
      azureActiveDirectory: {
        enabled: true
        isAutoProvisioned: false
        registration: {
          clientId: entraAccessGateClientId
          clientSecretSettingName: 'entra-access-gate-client-secret'
          openIdIssuer: '${environment().authentication.loginEndpoint}${entraTenantId}/v2.0'
        }
        validation: {
          allowedAudiences: [
            entraAccessGateClientId
          ]
        }
      }
    }
    login: {
      allowedExternalRedirectUrls: [
        publicOrigin
      ]
      cookieExpiration: {
        convention: 'FixedTime'
        timeToExpiration: '08:00:00'
      }
      nonce: {
        nonceExpirationInterval: '00:05:00'
        validateNonce: true
      }
      preserveUrlFragmentsForLogins: true
      tokenStore: {
        enabled: false
      }
    }
    platform: {
      enabled: true
    }
  }
}

output backendName string = backend.name
output backendFqdn string = backend.properties.configuration.ingress.fqdn
output clamAvName string = clamAv.name
output clamAvFqdn string = clamAv.properties.configuration.ingress.fqdn
output frontendName string = frontend.name
output frontendFqdn string = frontend.properties.configuration.ingress.fqdn
output migrationJobName string = migrationJob.name
output databaseBootstrapJobName string = databaseBootstrapJob.name
output workloadsActivated bool = activateWorkloads
output externalNotificationsEnabled bool = enableExternalNotifications
output frontendPreAuthenticationEnabled bool = true
