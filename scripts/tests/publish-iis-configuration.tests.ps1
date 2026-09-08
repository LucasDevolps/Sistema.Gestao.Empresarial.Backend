#Requires -Version 5.1
# Exercises only the actual configuration functions; never runs deployment or reads .env.
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$source = Join-Path (Split-Path -Parent $PSScriptRoot) 'publish-local-iis.ps1'
$tokens = $null
$parseErrors = $null
$ast = [System.Management.Automation.Language.Parser]::ParseFile($source, [ref]$tokens, [ref]$parseErrors)
if ($parseErrors.Count) { throw ($parseErrors.Message -join '; ') }
foreach ($name in @('Assert-PublicOrigins', 'Protect-Secret', 'Write-Log', 'Write-FrontendWebConfig', 'Write-FrontendRuntimeConfig', 'Get-ApiEnvironmentVariables')) {
    $definition = $ast.Find({ param($node) $node -is [System.Management.Automation.Language.FunctionDefinitionAst] -and $node.Name -eq $name }, $true)
    if ($null -eq $definition) { throw "Missing function: $name" }
    . ([scriptblock]::Create($definition.Extent.Text))
}
function Assert-Condition([bool]$Condition, [string]$Message) {
    if (-not $Condition) { throw $Message }
}
$script:LogDir = Join-Path ([IO.Path]::GetTempPath()) ('sge-iis-config-test-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $script:LogDir | Out-Null
$script:LogFile = Join-Path $script:LogDir 'validation.log'
$script:SecretValues = @()
$DryRun = $false
$BackendPort = 9081
$SqlServerLoopbackPort = 11433
$RabbitMqLoopbackPort = 15673
$RedisLoopbackPort = 16379
$OtlpGrpcLoopbackPort = 14317
$emptyEnvironment = @{}
$xmlPath = Join-Path $script:LogDir 'web.config'
$jsonPath = Join-Path $script:LogDir 'config.json'

# .test names are reserved test inputs, never suggested deployment domains.
foreach ($public in @($false, $true)) {
    $PublicFrontendOrigin = if ($public) { 'https://frontend.example.test' } else { '' }
    $PublicBackendOrigin = if ($public) { 'https://backend.example.test' } else { '' }
    Assert-PublicOrigins
    Assert-Condition ($script:PublicMode -eq $public) 'Wrong deployment mode'
    Write-FrontendWebConfig -Path $xmlPath
    [xml]$xml = Get-Content -LiteralPath $xmlPath -Raw
    Assert-Condition ($null -eq $xml.SelectSingleNode('//hiddenSegments/add[@segment="config.json"]')) 'Runtime config is hidden'
    Assert-Condition ($null -ne $xml.SelectSingleNode('/configuration/location[@path="config.json"]//clientCache[@cacheControlMode="DisableCache"]')) 'Runtime config may be cached'
    Assert-Condition ($null -ne $xml.SelectSingleNode('/configuration/location[@path="index.html"]//clientCache[@cacheControlMode="DisableCache"]')) 'SPA entrypoint may be stale'
    $csp = $xml.SelectSingleNode('//customHeaders/add[@name="Content-Security-Policy"]').value
    Assert-Condition ($csp.Contains("script-src 'self'")) 'Missing CSP script protection'
    Assert-Condition ($csp.Contains('https://backend.example.test') -eq $public) 'Incorrect CSP API origin'
    Assert-Condition (($null -ne $xml.SelectSingleNode('//rule[@name="SGE public API uses its own hostname"]')) -eq $public) 'Public frontend unexpectedly proxies API traffic'
    Assert-Condition ($xml.SelectSingleNode('//rule[@name="SGE API proxy"]/action').url -eq 'http://127.0.0.1:9081/{R:0}') 'Local API port changed'
    Set-Content -LiteralPath $jsonPath -Value '{"apiBaseUrl":"/api","sessionExpiryWarningSeconds":120}' -Encoding UTF8
    Write-FrontendRuntimeConfig -Path $jsonPath
    $config = Get-Content -LiteralPath $jsonPath -Raw | ConvertFrom-Json
    Assert-Condition ($config.sessionExpiryWarningSeconds -eq 120) 'Session warning changed'
    if ($public) {
        Assert-Condition ($config.apiBaseUrl -eq 'https://backend.example.test/api') 'Wrong public API URL'
        Assert-Condition ($config.localApiBaseUrl -eq '/api') 'Local IIS lost its same-origin API'
    } else {
        Assert-Condition ($config.apiBaseUrl -eq '/api') 'Local API changed'
    }
    $variables = Get-ApiEnvironmentVariables -Env $emptyEnvironment
    Assert-Condition ($variables['ReverseProxy__KnownProxies__1'] -eq '::1') 'IPv6 loopback missing'
    Assert-Condition ($variables['ReverseProxy__UseCloudflareHeaders'] -eq "$public".ToLowerInvariant()) 'Wrong header profile'
    Assert-Condition ($variables['Swagger__Enabled'] -eq 'false') 'Swagger is enabled'
    Assert-Condition ($variables.Contains('Cors__AllowedOrigins__0') -eq $public) 'Wrong CORS mode'
    if ($public) {
        Assert-Condition ($variables['Cors__AllowedOrigins__0'] -eq $PublicFrontendOrigin) 'Wrong CORS origin'
        Assert-Condition ($variables['AllowedHosts'] -eq 'localhost;backend.example.test') 'Host filtering is not restricted'
    }
}
foreach ($invalid in @('https://app.sistema-gerenciador-empresarial', 'https://app.sistema-gerenciador-empresarial-backend', 'http://frontend.example.test', 'https://localhost', 'https://frontend.example.test/path', 'https://frontend.example.test:444', '')) {
    $PublicFrontendOrigin = $invalid
    $PublicBackendOrigin = 'https://backend.example.test'
    $rejected = $false
    try { Assert-PublicOrigins } catch { $rejected = $true }
    Assert-Condition $rejected 'Unsafe or incomplete public configuration was accepted'
}
Write-Output 'PASS: local/public runtime configuration, XML, CSP, cache, ports, CORS, host filtering, proxy profile and invalid origins.'
Write-Output "Validation artifacts: $script:LogDir"
