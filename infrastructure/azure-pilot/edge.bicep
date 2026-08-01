targetScope = 'resourceGroup'

@description('Front Door profile name from foundation.bicep.')
param frontDoorProfileName string

@description('Front Door endpoint name from foundation.bicep.')
param frontDoorEndpointName string

@description('Front Door rule set name from foundation.bicep.')
param frontDoorRuleSetName string = 'originverification'

@description('WAF policy resource ID from foundation.bicep.')
param wafPolicyId string

@description('Frontend Container App fully qualified domain name from workloads.bicep.')
param frontendFqdn string

@description('Exact public hostname. Pass at deployment time; do not commit the real value.')
param publicHost string

@description('Stable Azure resource name for the custom domain.')
param customDomainResourceName string = 'pms-domain'

resource frontDoorProfile 'Microsoft.Cdn/profiles@2024-09-01' existing = {
  name: frontDoorProfileName
}

resource frontDoorEndpoint 'Microsoft.Cdn/profiles/afdEndpoints@2024-09-01' existing = {
  parent: frontDoorProfile
  name: frontDoorEndpointName
}

resource originVerificationRuleSet 'Microsoft.Cdn/profiles/ruleSets@2024-09-01' existing = {
  parent: frontDoorProfile
  name: frontDoorRuleSetName
}

resource originGroup 'Microsoft.Cdn/profiles/originGroups@2024-09-01' = {
  parent: frontDoorProfile
  name: 'frontend'
  properties: {
    healthProbeSettings: {
      probeIntervalInSeconds: 30
      probePath: '/health/live'
      probeProtocol: 'Https'
      probeRequestType: 'HEAD'
    }
    loadBalancingSettings: {
      additionalLatencyInMilliseconds: 50
      sampleSize: 4
      successfulSamplesRequired: 3
    }
    sessionAffinityState: 'Disabled'
  }
}

resource frontendOrigin 'Microsoft.Cdn/profiles/originGroups/origins@2024-09-01' = {
  parent: originGroup
  name: 'container-app-frontend'
  properties: {
    enabledState: 'Enabled'
    enforceCertificateNameCheck: true
    hostName: frontendFqdn
    httpPort: 80
    httpsPort: 443
    originHostHeader: frontendFqdn
    priority: 1
    weight: 1000
  }
}

resource customDomain 'Microsoft.Cdn/profiles/customDomains@2024-09-01' = {
  parent: frontDoorProfile
  name: customDomainResourceName
  properties: {
    hostName: publicHost
    tlsSettings: {
      certificateType: 'ManagedCertificate'
      minimumTlsVersion: 'TLS12'
    }
  }
}

resource route 'Microsoft.Cdn/profiles/afdEndpoints/routes@2024-09-01' = {
  parent: frontDoorEndpoint
  name: 'public'
  properties: {
    cacheConfiguration: null
    customDomains: [
      {
        id: customDomain.id
      }
    ]
    enabledState: 'Enabled'
    forwardingProtocol: 'HttpsOnly'
    httpsRedirect: 'Enabled'
    linkToDefaultDomain: 'Disabled'
    originGroup: {
      id: originGroup.id
    }
    patternsToMatch: [
      '/*'
    ]
    ruleSets: [
      {
        id: originVerificationRuleSet.id
      }
    ]
    supportedProtocols: [
      'Http'
      'Https'
    ]
  }
  dependsOn: [
    frontendOrigin
  ]
}

resource securityPolicy 'Microsoft.Cdn/profiles/securityPolicies@2024-09-01' = {
  parent: frontDoorProfile
  name: 'public-waf'
  properties: {
    parameters: {
      associations: [
        {
          domains: [
            {
              id: customDomain.id
            }
          ]
          patternsToMatch: [
            '/*'
          ]
        }
      ]
      type: 'WebApplicationFirewall'
      wafPolicy: {
        id: wafPolicyId
      }
    }
  }
}

output customDomainId string = customDomain.id
output customDomainHost string = customDomain.properties.hostName
output customDomainValidationToken string = customDomain.properties.validationProperties.validationToken
output routeId string = route.id
output securityPolicyId string = securityPolicy.id
