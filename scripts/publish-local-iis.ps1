#Requires -Version 5.1
<#
.SYNOPSIS
  Publica a aplicação (API .NET 10 + SPA Angular) no IIS do Windows, de forma
  idempotente e SEM tocar no ambiente de desenvolvimento (Docker/WSL/IDE/Aspire).

.DESCRIPTION
  A aplicação publicada COEXISTE com o ambiente de desenvolvimento:

    Desenvolvimento (inalterado)         Publicação IIS (adicional)
    ----------------------------         --------------------------
    ng serve      -> localhost:4200      Frontend IIS -> 127.0.0.1:9080
    Nginx (Docker)-> localhost:8080/8443 Backend  IIS -> 127.0.0.1:9081
                                         (mesmos SQL/Redis/RabbitMQ/OTEL do Docker)

  A infraestrutura (SQL Server, Redis, RabbitMQ, OpenTelemetry Collector) continua
  sendo a MESMA que já roda no Docker dentro do WSL2. Este script apenas publica
  as portas dessa infraestrutura em 127.0.0.1 (loopback do WSL) através do overlay
  docker-compose.iis.yml — nunca em 0.0.0.0, nunca na LAN.

  O ambiente publicado usa DOTNET_ENVIRONMENT=Production:
    * Swagger permanece desativado (Swagger:Enabled=false em Production).
    * Developer Exception Page desativada; sem stack trace exposto.

  MIGRAÇÕES: este script NÃO executa migrations. O projeto aplica migrations
  fora do processo (scripts/apply-migrations-docker.sh), então API da IDE e API
  do IIS nunca disputam o schema. O script apenas VALIDA que o banco já tem
  schema (scripts/_db-has-schema.sh) e aborta caso não tenha.

.PARAMETER Action
  Publish (padrão) | Start | Stop | Status | Remove

.PARAMETER FrontendPort
  Porta HTTP (loopback) do site IIS do frontend. Padrão 9080.

.PARAMETER BackendPort
  Porta HTTP (loopback) do site IIS da API. Padrão 9081.

.PARAMETER PublicFrontendOrigin
  Origem HTTPS real do frontend. Também aceita SGE_PUBLIC_FRONTEND_ORIGIN.
  Informe junto com PublicBackendOrigin para o perfil Cloudflare Tunnel local.

.PARAMETER PublicBackendOrigin
  Origem HTTPS real da API, sem /api. Também aceita SGE_PUBLIC_BACKEND_ORIGIN.
  Sem as duas origens, preserva a publicação local de mesma origem.

.PARAMETER FrontendPath
  Caminho do repositório do frontend Angular. Padrão:
  C:\Users\lucas\source\repos\Sistema.Gestao.Empresarial.Frontend

.PARAMETER BackendPath
  Raiz do repositório do backend. Padrão: pasta acima de \scripts.

.PARAMETER IisRoot
  Raiz física dos sites publicados. Padrão C:\inetpub\SistemaGestaoEmpresarial
  (subpastas \Api e \Frontend).

.PARAMETER WslDistribution
  Distribuição WSL onde o Docker roda. Padrão Ubuntu.

.PARAMETER SqlServerLoopbackPort / RedisLoopbackPort / RabbitMqLoopbackPort /
           OtlpGrpcLoopbackPort / OtlpHttpLoopbackPort
  Portas host (127.0.0.1) publicadas pelo overlay docker-compose.iis.yml.
  Padrões: 11433 / 16379 / 15673 / 14317 / 14318.

.PARAMETER HostingBundleInstaller / UrlRewriteInstaller / ArrInstaller
  Caminho local para instaladores já baixados (uso offline). Se omitido, o
  script baixa de fonte oficial Microsoft e valida a assinatura Authenticode.

.PARAMETER SkipTests
  Pula 'dotnet test' / 'ng test' (NÃO recomendado; use só em republicações rápidas).

.PARAMETER DryRun
  Analisa e valida tudo, mostra o que seria feito e NÃO altera nada
  (não instala, não cria sites/pools, não sobe overlay, não publica arquivos).

.EXAMPLE
  # Primeira publicação / atualização (PowerShell COMO ADMINISTRADOR)
  .\scripts\publish-local-iis.ps1 -FrontendPort 9080 -BackendPort 9081

.EXAMPLE
  .\scripts\publish-local-iis.ps1 -DryRun

.EXAMPLE
  .\scripts\publish-local-iis.ps1 -Action Status
  .\scripts\publish-local-iis.ps1 -Action Stop
  .\scripts\publish-local-iis.ps1 -Action Start
  .\scripts\publish-local-iis.ps1 -Action Remove
#>
[CmdletBinding()]
param(
    [ValidateSet('Publish', 'Start', 'Stop', 'Status', 'Remove')]
    [string]$Action = 'Publish',

    [ValidateRange(1, 65535)] [int]$FrontendPort = 9080,
    [ValidateRange(1, 65535)] [int]$BackendPort = 9081,

    [string]$PublicFrontendOrigin = $env:SGE_PUBLIC_FRONTEND_ORIGIN,
    [string]$PublicBackendOrigin = $env:SGE_PUBLIC_BACKEND_ORIGIN,

    [string]$FrontendPath = 'C:\Users\lucas\source\repos\Sistema.Gestao.Empresarial.Frontend',
    [string]$BackendPath,
    [string]$IisRoot = 'C:\inetpub\SistemaGestaoEmpresarial',
    [string]$WslDistribution = 'Ubuntu',

    [ValidateRange(1, 65535)] [int]$SqlServerLoopbackPort = 11433,
    [ValidateRange(1, 65535)] [int]$RedisLoopbackPort = 16379,
    [ValidateRange(1, 65535)] [int]$RabbitMqLoopbackPort = 15673,
    [ValidateRange(1, 65535)] [int]$OtlpGrpcLoopbackPort = 14317,
    [ValidateRange(1, 65535)] [int]$OtlpHttpLoopbackPort = 14318,

    [string]$HostingBundleInstaller,
    [string]$UrlRewriteInstaller,
    [string]$ArrInstaller,

    [switch]$SkipTests,
    [switch]$DryRun
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# wsl.exe emite texto em UTF-16LE por padrão; no Windows PowerShell 5.1 isso vira
# mojibake ("U b u n t u") ao capturar a saída. WSL_UTF8=1 força UTF-8 e cobre
# tanto 'wsl -l -q' quanto as mensagens de erro do próprio wsl.exe.
$env:WSL_UTF8 = '1'

# ===========================================================================
#  Constantes
# ===========================================================================
$script:ApiSiteName = 'SistemaGestaoEmpresarial-Api'
$script:ApiPoolName = 'SistemaGestaoEmpresarial-Api'
$script:FrontendSiteName = 'SistemaGestaoEmpresarial-Frontend'
$script:FrontendPoolName = 'SistemaGestaoEmpresarial-Frontend'

# Fontes OFICIAIS Microsoft (usadas só se o instalador não for fornecido em -*Installer)
$script:HostingBundleUrl = 'https://aka.ms/dotnet/10.0/dotnet-hosting-win.exe'
$script:UrlRewriteUrl = 'https://download.microsoft.com/download/1/2/8/128E2E22-C1B9-44A4-BE2A-5859ED1D4592/rewrite_amd64_en-US.msi'
$script:ArrUrl = 'https://download.microsoft.com/download/E/9/8/E9849D6A-020E-47E4-9FD0-A023E99B54EB/requestRouter_amd64.msi'
$script:TrustedInstallerHosts = @(
    'aka.ms', 'dotnet.microsoft.com', 'download.microsoft.com',
    'download.visualstudio.microsoft.com', 'builds.dotnet.microsoft.com'
)

# Nomes das variáveis de segredo esperadas no .env (valores NUNCA são logados).
$script:RequiredSecretKeys = @(
    'SGE_SQLSERVER_APP_USERNAME', 'SGE_SQLSERVER_APP_PASSWORD',
    'SGE_REDIS_USERNAME', 'SGE_REDIS_PASSWORD',
    'SGE_RABBITMQ_USERNAME', 'SGE_RABBITMQ_PASSWORD',
    'SGE_JWT_SIGNING_KEY'
)

# ===========================================================================
#  Infra de log (com mascaramento de segredos)
# ===========================================================================
$script:BackendRoot = if ($BackendPath) { $BackendPath } else { Split-Path -Parent $PSScriptRoot }
$script:LogDir = Join-Path $script:BackendRoot 'logs'
$script:LogFile = Join-Path $script:LogDir ("publish-iis-{0}.log" -f (Get-Date -Format 'yyyyMMdd-HHmmss'))
$script:SecretValues = @()   # preenchido depois que o .env é lido
$script:PublicMode = $false

function Assert-PublicOrigins {
    if ([string]::IsNullOrWhiteSpace($PublicFrontendOrigin) -and [string]::IsNullOrWhiteSpace($PublicBackendOrigin)) {
        $script:PublicMode = $false
        return
    }
    foreach ($origin in @($PublicFrontendOrigin, $PublicBackendOrigin)) {
        $uri = $null
        if (-not [Uri]::TryCreate($origin, [UriKind]::Absolute, [ref]$uri) -or
            $uri.Scheme -ne 'https' -or $uri.Port -ne 443 -or $uri.UserInfo -or
            $uri.AbsolutePath -ne '/' -or $uri.Query -or $uri.Fragment -or
            $uri.HostNameType -ne [UriHostNameType]::Dns -or $uri.IsLoopback -or
            $origin -ne $uri.GetLeftPart([UriPartial]::Authority) -or
            $uri.Host -notmatch '^(?:[a-z0-9](?:[a-z0-9-]*[a-z0-9])?\.)+(?:[a-z]{2,63}|xn--[a-z0-9-]+)$') {
            throw 'Informe ambas as origens HTTPS com FQDN real, sem porta alternativa, caminho, credenciais, query ou fragmento. Os hostnames provisórios do pedido não são domínios públicos.'
        }
    }
    if ($PublicFrontendOrigin -eq $PublicBackendOrigin) { throw 'Informe origens distintas para frontend e backend neste perfil.' }
    $script:PublicMode = $true
}

function Protect-Secret {
    param([string]$Text)
    if ([string]::IsNullOrEmpty($Text)) { return $Text }
    $out = $Text
    foreach ($secret in $script:SecretValues) {
        if ($secret -and $secret.Length -ge 3) {
            $out = $out.Replace($secret, '***')
        }
    }
    # Mascara senhas embutidas em connection strings, ainda que o valor não esteja no set.
    $out = [regex]::Replace($out, '(?i)(Password|Pwd|SigningKey|AccessKey|ApiKey)\s*=\s*("?)([^";,\s]+)\2', '$1=$2***$2')
    $out = [regex]::Replace($out, '(?i)(password=)([^,;"\s]+)', '$1***')
    return $out
}

function Write-Log {
    param(
        [string]$Message,
        [ValidateSet('INFO', 'STEP', 'WARN', 'ERROR', 'OK', 'DRY')] [string]$Level = 'INFO'
    )
    $masked = Protect-Secret $Message
    $line = "{0} [{1}] {2}" -f (Get-Date -Format 'HH:mm:ss'), $Level, $masked
    if (-not (Test-Path $script:LogDir)) { New-Item -ItemType Directory -Force -Path $script:LogDir | Out-Null }
    Add-Content -Path $script:LogFile -Value $line -Encoding UTF8
    $color = switch ($Level) {
        'STEP' { 'Cyan' } 'WARN' { 'Yellow' } 'ERROR' { 'Red' }
        'OK' { 'Green' } 'DRY' { 'Magenta' } default { 'Gray' }
    }
    Write-Host $line -ForegroundColor $color
}

function Write-Step { param([string]$m) Write-Log -Level STEP -Message "==== $m ====" }

function Invoke-OrDryRun {
    param([string]$Description, [scriptblock]$Action)
    if ($DryRun) {
        Write-Log -Level DRY -Message "[dry-run] $Description"
        return
    }
    Write-Log -Level INFO -Message $Description
    & $Action
}

# ===========================================================================
#  Helpers gerais
# ===========================================================================
function Assert-Administrator {
    $id = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = New-Object Security.Principal.WindowsPrincipal($id)
    if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
        throw "Este script precisa ser executado em um PowerShell ELEVADO (Executar como Administrador). Abortando."
    }
    Write-Log -Level OK -Message "Sessão elevada confirmada ($($id.Name))."
}

function Import-WebAdministration {
    if (-not (Get-Module -ListAvailable -Name WebAdministration)) {
        throw "Módulo WebAdministration ausente. O IIS Management Console (IIS-ManagementScriptingTools) não está instalado."
    }
    Import-Module WebAdministration -ErrorAction Stop
}

function Invoke-Wsl {
    param([string]$Bash, [switch]$AllowFail)
    # 'docker compose' (e outros) escrevem progresso em stderr. Com 2>&1 e
    # $ErrorActionPreference='Stop', o Windows PowerShell 5.1 promove a 1ª linha
    # de stderr de um comando nativo a erro TERMINANTE (NativeCommandError) — isso
    # dispararia antes mesmo de checarmos -AllowFail. Localmente usamos 'Continue'
    # para que stderr seja apenas texto; sucesso/falha vem do exit code.
    $prevEap = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    try {
        $out = & wsl.exe -d $WslDistribution -- bash -lc $Bash 2>&1
        $code = $LASTEXITCODE
    } finally {
        $ErrorActionPreference = $prevEap
    }
    $text = (@($out) | ForEach-Object { $_.ToString() }) -join "`n"
    if ($code -ne 0 -and -not $AllowFail) {
        throw "Comando WSL falhou (exit $code): $Bash`n$text"
    }
    return [pscustomobject]@{ Code = $code; Output = $text }
}

function ConvertTo-WslPath {
    param([string]$WindowsPath)
    $r = Invoke-Wsl "wslpath -a '$($WindowsPath -replace "'", "''")'"
    return $r.Output.Trim()
}

function Get-PortOwnerDescription {
    param([int]$Port)
    $desc = @()
    try {
        $conns = Get-NetTCPConnection -LocalPort $Port -State Listen -ErrorAction SilentlyContinue
        foreach ($c in $conns) {
            $p = Get-Process -Id $c.OwningProcess -ErrorAction SilentlyContinue
            $desc += "processo Windows '{0}' (PID {1}) em {2}:{3}" -f `
            ($(if ($p) { $p.ProcessName } else { '?' })), $c.OwningProcess, $c.LocalAddress, $Port
        }
    } catch {}
    try {
        Import-Module WebAdministration -ErrorAction SilentlyContinue
        foreach ($site in (Get-Website -ErrorAction SilentlyContinue)) {
            foreach ($b in $site.Bindings.Collection) {
                if ($b.bindingInformation -match ":$Port`:") {
                    $desc += "site IIS '{0}' (binding {1})" -f $site.Name, $b.bindingInformation
                }
            }
        }
    } catch {}
    $docker = Invoke-Wsl "docker ps --format '{{.Names}} => {{.Ports}}' | grep -E ':$Port(->| |$)' || true" -AllowFail
    if ($docker.Output.Trim()) { $desc += "container Docker/WSL: $($docker.Output.Trim())" }
    if (-not $desc) { $desc += "origem não identificada (porta ocupada)" }
    return ($desc -join ' ; ')
}

function Assert-PortFreeForUs {
    param([int]$Port, [string]$Purpose, [string[]]$OwnedBy = @())
    # "OwnedBy": nomes de site IIS que PODEM ocupar a porta (é a nossa própria publicação).
    $conns = Get-NetTCPConnection -LocalPort $Port -State Listen -ErrorAction SilentlyContinue
    if (-not $conns) { Write-Log -Level OK -Message "Porta $Port livre ($Purpose)."; return }

    # Se a porta já é nossa (republicação), tudo bem.
    try {
        Import-Module WebAdministration -ErrorAction SilentlyContinue
        foreach ($name in $OwnedBy) {
            $site = Get-Website -Name $name -ErrorAction SilentlyContinue
            if ($site) {
                foreach ($b in $site.Bindings.Collection) {
                    if ($b.bindingInformation -match ":$Port`:") {
                        Write-Log -Level OK -Message "Porta $Port já pertence ao site '$name' (republicação). OK."
                        return
                    }
                }
            }
        }
    } catch {}

    $owner = Get-PortOwnerDescription -Port $Port
    throw "Porta $Port ($Purpose) já está EM USO por: $owner. O script NÃO escolhe outra porta silenciosamente. Libere a porta ou informe outra em -$($Purpose)."
}

# ===========================================================================
#  .env / segredos
# ===========================================================================
function Read-DotEnv {
    $envFile = Join-Path $script:BackendRoot '.env'
    if (-not (Test-Path $envFile)) {
        throw "Arquivo .env não encontrado em '$envFile'. Crie-o a partir de .env.example (mesma estratégia usada pelo Docker/dev)."
    }
    $map = @{}
    foreach ($raw in Get-Content $envFile) {
        $line = $raw.Trim()
        if (-not $line -or $line.StartsWith('#')) { continue }
        $idx = $line.IndexOf('=')
        if ($idx -lt 1) { continue }
        $key = $line.Substring(0, $idx).Trim()
        $val = $line.Substring($idx + 1).Trim()
        if ($val.Length -ge 2 -and (($val[0] -eq '"' -and $val[-1] -eq '"') -or ($val[0] -eq "'" -and $val[-1] -eq "'"))) {
            $val = $val.Substring(1, $val.Length - 2)
        }
        $map[$key] = $val
    }
    # Alimenta o mascarador com TODOS os valores não triviais do .env.
    $script:SecretValues = @($map.Values | Where-Object { $_ -and $_.Length -ge 4 })
    return $map
}

function Assert-RequiredSecrets {
    param([hashtable]$EnvMap)
    $missing = @()
    foreach ($k in $script:RequiredSecretKeys) {
        if (-not $EnvMap.ContainsKey($k) -or [string]::IsNullOrWhiteSpace($EnvMap[$k]) -or $EnvMap[$k] -like 'CHANGE_ME*') {
            $missing += $k
            Write-Log -Level ERROR -Message "Segredo ausente/placeholder: $k"
        } else {
            Write-Log -Level OK -Message "Segredo presente: $k"   # valor NUNCA é impresso
        }
    }
    if ($missing.Count) {
        throw "Segredos obrigatórios ausentes no .env: $($missing -join ', '). Configure-os (mesma estratégia do ambiente de dev) e rode novamente."
    }
}

# ===========================================================================
#  WSL / Docker / infraestrutura
# ===========================================================================
function Test-WslReady {
    Write-Step "1/13  WSL e distribuição '$WslDistribution'"
    $list = (& wsl.exe -l -q) -join "`n"
    if ($list -notmatch [regex]::Escape($WslDistribution)) {
        throw "Distribuição WSL '$WslDistribution' não encontrada. Distros: $($list -replace "`n", ', '). Informe -WslDistribution."
    }
    $probe = Invoke-Wsl "echo ok" -AllowFail
    if ($probe.Code -ne 0 -or $probe.Output.Trim() -ne 'ok') {
        throw "Não foi possível executar comandos em '$WslDistribution'. A distro está iniciada? (wsl -d $WslDistribution)"
    }
    Write-Log -Level OK -Message "WSL '$WslDistribution' respondendo."
}

function Test-DockerReady {
    Write-Step "2/13  Docker dentro do WSL"
    $d = Invoke-Wsl "docker info --format '{{.ServerVersion}}'" -AllowFail
    if ($d.Code -ne 0) {
        throw "Docker não está disponível dentro de '$WslDistribution'. Suba o ambiente de dev primeiro (scripts/dev-up.ps1)."
    }
    Write-Log -Level OK -Message "Docker Engine $($d.Output.Trim()) ativo no WSL."
    $repoWsl = ConvertTo-WslPath $script:BackendRoot
    $script:BackendRootWsl = $repoWsl
    # 'wsl.exe' pode bouncear os containers (~15s) neste build; toleramos alguns
    # ciclos antes de considerar a infra fora do ar.
    $pending = @('sqlserver', 'redis', 'rabbitmq', 'otel-collector')
    foreach ($try in 1..6) {
        $ps = Invoke-Wsl "cd '$repoWsl' && docker compose ps --format '{{.Service}} {{.State}} {{.Health}}'" -AllowFail
        Write-Log -Level INFO -Message "Estado do compose (tentativa $try):`n$($ps.Output)"
        $pending = @($pending | Where-Object { $ps.Output -notmatch "(?m)^$_\s+running" })
        if ($pending.Count -eq 0) { break }
        Write-Log -Level WARN -Message "Aguardando serviço(s) '$($pending -join ', ')' voltarem a 'running' (8s)..."
        Start-Sleep -Seconds 8
    }
    if ($pending.Count -ne 0) {
        throw "Serviço(s) de infraestrutura '$($pending -join ', ')' não estão 'running' no Docker. Suba o ambiente de dev (scripts/dev-up.ps1) e rode novamente."
    }
    Write-Log -Level OK -Message "Infraestrutura de desenvolvimento (SQL/Redis/RabbitMQ/OTEL) está de pé."
}

function Start-IisInfrastructurePorts {
    Write-Step "3/13  Publicar portas de infraestrutura em 127.0.0.1 (overlay docker-compose.iis.yml)"
    $repoWsl = $script:BackendRootWsl
    $envExports = @(
        "SGE_IIS_SQLSERVER_PORT=$SqlServerLoopbackPort",
        "SGE_IIS_REDIS_PORT=$RedisLoopbackPort",
        "SGE_IIS_RABBITMQ_PORT=$RabbitMqLoopbackPort",
        "SGE_IIS_OTLP_GRPC_PORT=$OtlpGrpcLoopbackPort",
        "SGE_IIS_OTLP_HTTP_PORT=$OtlpHttpLoopbackPort"
    ) -join ' '
    $composeCmd = "cd '$repoWsl' && $envExports docker compose " +
    "-f docker-compose.yml -f docker-compose.override.yml -f docker-compose.iis.yml " +
    "up -d --no-deps sqlserver redis rabbitmq otel-collector"

    Invoke-OrDryRun "Aplicar overlay: $composeCmd" {
        # 'docker compose up' pode retornar não-zero se um re-init do WSL bouncear
        # a distro no meio da recriação (a rede/containers ficam parcialmente
        # aplicados). É idempotente: repetir conclui a operação.
        $applied = $false
        foreach ($try in 1..4) {
            $r = Invoke-Wsl $composeCmd -AllowFail
            Write-Log -Level INFO -Message "docker compose up (tentativa $try) -> exit $($r.Code):`n$($r.Output)"
            if ($r.Code -eq 0) { $applied = $true; break }
            Write-Log -Level WARN -Message "Overlay não aplicou por completo (provável bounce do WSL). Nova tentativa em 10s..."
            Start-Sleep -Seconds 10
        }
        if (-not $applied) { throw "Falha ao aplicar docker-compose.iis.yml após múltiplas tentativas." }
    }
    if ($DryRun) { return }

    # Confirma o NAT loopback WSL->Windows.
    foreach ($p in @(
            @{ n = 'SQL Server'; port = $SqlServerLoopbackPort },
            @{ n = 'Redis'; port = $RedisLoopbackPort },
            @{ n = 'RabbitMQ AMQP'; port = $RabbitMqLoopbackPort },
            @{ n = 'OTLP gRPC'; port = $OtlpGrpcLoopbackPort }
        )) {
        $ok = $false
        foreach ($try in 1..15) {
            $t = Test-NetConnection -ComputerName 127.0.0.1 -Port $p.port -WarningAction SilentlyContinue
            if ($t.TcpTestSucceeded) { $ok = $true; break }
            Start-Sleep -Seconds 2
        }
        if (-not $ok) {
            throw "Windows não conseguiu alcançar $($p.n) em 127.0.0.1:$($p.port). Verifique o NAT loopback do WSL2 (localhostForwarding=true em .wslconfig)."
        }
        Write-Log -Level OK -Message "$($p.n) acessível pelo Windows em 127.0.0.1:$($p.port)."
    }
}

function Test-DatabaseSchema {
    Write-Step "4/13  Validar banco (MESMO banco do dev; NÃO cria, NÃO migra)"
    $repoWsl = $script:BackendRootWsl
    # Cada 'wsl.exe' pode disparar um re-init do systemd (bounce gracioso dos
    # containers ~15s) neste build do WSL. O check é somente-leitura, então
    # repetimos algumas vezes: só é falha real se NUNCA obtivermos schema.
    $r = $null
    foreach ($try in 1..6) {
        $r = Invoke-Wsl "cd '$repoWsl' && bash ./scripts/_db-has-schema.sh" -AllowFail
        Write-Log -Level INFO -Message "scripts/_db-has-schema.sh (tentativa $try) -> exit $($r.Code): $(Protect-Secret $r.Output)"
        if ($r.Code -eq 0) { break }
        if ($r.Output -match 'cannot be autostarted|shutdown or startup|Login failed|not currently available|error: 4060|Msg (911|18456|4060|904)') {
            Write-Log -Level WARN -Message "SQL Server ainda inicializando (bounce do WSL). Nova tentativa em 8s..."
            Start-Sleep -Seconds 8
            continue
        }
        break   # erro que não parece transitório
    }
    if ($r.Code -ne 0) {
        throw "O banco 'SistemaGestaoEmpresarial' ainda não tem schema aplicado (ou o SQL Server não estabilizou). Aplique as migrations pelo fluxo existente (scripts/apply-migrations-docker.sh ou dev-up.ps1) e rode novamente. Este script nunca cria/migra/recria o banco."
    }
    Write-Log -Level OK -Message "Banco existente com __EFMigrationsHistory populada. Nenhuma migration será executada."
}

# ===========================================================================
#  IIS + Hosting Bundle + URL Rewrite + ARR
# ===========================================================================
$script:RequiredIisFeatures = @(
    'IIS-WebServerRole', 'IIS-WebServer', 'IIS-CommonHttpFeatures',
    'IIS-StaticContent', 'IIS-DefaultDocument', 'IIS-HttpErrors',
    'IIS-RequestFiltering', 'IIS-Security',
    'IIS-NetFxExtensibility45', 'IIS-ISAPIExtensions', 'IIS-ISAPIFilter',
    'IIS-WebSockets',
    'IIS-ManagementConsole', 'IIS-ManagementScriptingTools'
)

function Test-IisInstalled {
    $svc = Get-Service -Name W3SVC -ErrorAction SilentlyContinue
    $role = Get-WindowsOptionalFeature -Online -FeatureName 'IIS-WebServer' -ErrorAction SilentlyContinue
    return ($svc -and $role -and $role.State -eq 'Enabled')
}

function Install-Iis {
    Write-Step "5/13  IIS (Windows Optional Features)"
    $toEnable = @()
    foreach ($f in $script:RequiredIisFeatures) {
        $state = Get-WindowsOptionalFeature -Online -FeatureName $f -ErrorAction SilentlyContinue
        if (-not $state) { Write-Log -Level WARN -Message "Feature '$f' não existe nesta edição do Windows (ignorada)."; continue }
        if ($state.State -ne 'Enabled') { $toEnable += $f } else { Write-Log -Level OK -Message "Feature já habilitada: $f" }
    }
    if (-not $toEnable.Count) { Write-Log -Level OK -Message "Todos os recursos IIS necessários já estão habilitados." }
    else {
        Invoke-OrDryRun "Habilitar recursos IIS: $($toEnable -join ', ')" {
            Enable-WindowsOptionalFeature -Online -FeatureName $toEnable -All -NoRestart | Out-Null
        }
    }
    if ($DryRun) { return }
    foreach ($s in 'WAS', 'W3SVC') {
        $svc = Get-Service -Name $s -ErrorAction SilentlyContinue
        if (-not $svc) { throw "Serviço $s não encontrado após instalar o IIS." }
        if ($svc.Status -ne 'Running') { Start-Service $s }
        Set-Service -Name $s -StartupType Automatic
        Write-Log -Level OK -Message ("Serviço {0}: {1}." -f $s, (Get-Service $s).Status)
    }
}

function Get-DownloadedInstaller {
    param([string]$Url, [string]$Provided, [string]$Name)
    if ($Provided) {
        if (-not (Test-Path $Provided)) { throw "Instalador informado para $Name não existe: $Provided" }
        Write-Log -Level INFO -Message "Usando instalador local de ${Name}: $Provided"
        return (Resolve-Path $Provided).Path
    }
    $uri = [Uri]$Url
    if ($uri.Host -notin $script:TrustedInstallerHosts) {
        throw "URL de $Name não está em domínio Microsoft confiável: $($uri.Host). Baixe manualmente e passe em -${Name}Installer."
    }
    $ext = [IO.Path]::GetExtension($uri.AbsolutePath); if (-not $ext) { $ext = '.exe' }
    $dest = Join-Path $env:TEMP ("sge-iis-{0}{1}" -f ($Name -replace '\W', ''), $ext)
    Write-Log -Level INFO -Message "Baixando $Name de fonte oficial: $Url"
    [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
    Invoke-WebRequest -Uri $Url -OutFile $dest -UseBasicParsing -MaximumRedirection 5
    $sig = Get-AuthenticodeSignature -FilePath $dest
    if ($sig.Status -ne 'Valid' -or $sig.SignerCertificate.Subject -notmatch 'Microsoft Corporation') {
        Remove-Item $dest -Force -ErrorAction SilentlyContinue
        throw "Assinatura Authenticode de $Name inválida ou não-Microsoft (status: $($sig.Status)). Abortado."
    }
    Write-Log -Level OK -Message "$Name baixado e assinatura Microsoft verificada."
    return $dest
}

function Install-MsiOrExe {
    param([string]$Path, [string]$Name)
    $ext = [IO.Path]::GetExtension($Path).ToLowerInvariant()
    if ($ext -eq '.msi') {
        $p = Start-Process 'msiexec.exe' -ArgumentList "/i `"$Path`" /qn /norestart" -Wait -PassThru
    } else {
        $p = Start-Process $Path -ArgumentList "/install /quiet /norestart" -Wait -PassThru
    }
    if ($p.ExitCode -notin 0, 1638, 3010) { throw "$Name retornou exit code $($p.ExitCode)." }
    if ($p.ExitCode -eq 3010) { Write-Log -Level WARN -Message "$Name pede reboot (3010). Reinicie o Windows quando possível." }
    Write-Log -Level OK -Message "$Name instalado (exit $($p.ExitCode))."
}

function Test-AspNetCoreModule {
    $shared = 'C:\Program Files\dotnet\shared\Microsoft.AspNetCore.App'
    if (-not (Test-Path $shared)) { return $false }
    $net10 = Get-ChildItem $shared -Directory -ErrorAction SilentlyContinue | Where-Object { $_.Name -like '10.*' }
    if (-not $net10) { return $false }
    # ANCM v2: Hosting Bundles atuais NÃO copiam mais o shim para
    # %windir%\System32\inetsrv — registram o módulo apontando para
    # %ProgramFiles%\IIS\Asp.Net Core Module\V2\aspnetcorev2.dll. Aceitamos
    # qualquer um dos dois layouts, e por fim o registro no applicationHost.config.
    $dllModern = Join-Path $env:ProgramFiles 'IIS\Asp.Net Core Module\V2\aspnetcorev2.dll'
    $dllLegacy = Join-Path $env:windir 'System32\inetsrv\aspnetcorev2.dll'
    if ((Test-Path $dllModern) -or (Test-Path $dllLegacy)) { return $true }
    try {
        $cfg = Get-Content (Join-Path $env:windir 'System32\inetsrv\config\applicationHost.config') -Raw -ErrorAction Stop
        return ($cfg -match 'name="AspNetCoreModuleV2"\s+image=')
    } catch { return $false }
}

function Install-AspNetCoreHostingBundle {
    param([bool]$IisWasFreshlyInstalled)
    Write-Step "6/13  ASP.NET Core Module v2 / .NET 10 Hosting Bundle"
    $need = -not (Test-AspNetCoreModule)
    if ($need) { Write-Log -Level WARN -Message "ANCM v2 e/ou runtime ASP.NET Core 10 ausentes." }
    elseif ($IisWasFreshlyInstalled) {
        Write-Log -Level WARN -Message "IIS recém-instalado: Hosting Bundle será (re)parado para registrar o ANCM (recomendação Microsoft)."
        $need = $true
    } else { Write-Log -Level OK -Message "ANCM v2 e runtime ASP.NET Core 10 já presentes." }

    if (-not $need) { return }
    Invoke-OrDryRun "Baixar e instalar o .NET 10 Hosting Bundle (silencioso)" {
        $inst = Get-DownloadedInstaller -Url $script:HostingBundleUrl -Provided $HostingBundleInstaller -Name 'HostingBundle'
        Install-MsiOrExe -Path $inst -Name '.NET 10 Hosting Bundle'
        & net stop was /y 2>&1 | Out-Null
        & net start w3svc 2>&1 | Out-Null
        Start-Sleep -Seconds 2
    }
    if (-not $DryRun -and -not (Test-AspNetCoreModule)) {
        throw "Hosting Bundle instalado mas o ANCM/runtime .NET 10 ainda não foi detectado. Reinicie o Windows e rode novamente."
    }
}

function Install-ReverseProxyModules {
    Write-Step "7/13  URL Rewrite + Application Request Routing (proxy do frontend p/ API)"
    Import-WebAdministration
    $haveRewrite = [bool](Get-WebGlobalModule -ErrorAction SilentlyContinue | Where-Object Name -eq 'RewriteModule')
    $haveArr = [bool](Get-WebGlobalModule -ErrorAction SilentlyContinue | Where-Object Name -eq 'ApplicationRequestRouting')

    if ($haveRewrite) { Write-Log -Level OK -Message "URL Rewrite já instalado." }
    else {
        Invoke-OrDryRun "Instalar URL Rewrite (fonte oficial Microsoft)" {
            $i = Get-DownloadedInstaller -Url $script:UrlRewriteUrl -Provided $UrlRewriteInstaller -Name 'UrlRewrite'
            Install-MsiOrExe -Path $i -Name 'URL Rewrite'
        }
    }
    if ($haveArr) { Write-Log -Level OK -Message "ARR já instalado." }
    else {
        Invoke-OrDryRun "Instalar Application Request Routing (fonte oficial Microsoft)" {
            $i = Get-DownloadedInstaller -Url $script:ArrUrl -Provided $ArrInstaller -Name 'Arr'
            Install-MsiOrExe -Path $i -Name 'ARR'
        }
    }

    # ARR precisa do proxy habilitado para que o Rewrite encaminhe para URL absoluta.
    # Mantido restrito: nenhuma regra "catch-all" de saída; só /api e /health do site do frontend.
    Invoke-OrDryRun "Habilitar system.webServer/proxy (ARR) com preserveHostHeader" {
        & "$env:windir\system32\inetsrv\appcmd.exe" set config -section:system.webServer/proxy /enabled:"true" /preserveHostHeader:"true" /reverseRewriteHostInResponseHeaders:"false" /commit:apphost | Out-Null
    }
    if (-not $DryRun) {
        & net stop was /y 2>&1 | Out-Null
        & net start w3svc 2>&1 | Out-Null
    }
}

# ===========================================================================
#  Build + Publish
# ===========================================================================
function Resolve-ApiProject {
    $candidates = @(Get-ChildItem -Path (Join-Path $script:BackendRoot 'src') -Recurse -Filter '*.Api.csproj' -ErrorAction SilentlyContinue)
    if ($candidates.Count -eq 0) { throw "Projeto da API (*.Api.csproj) não encontrado em src/." }
    if ($candidates.Count -gt 1) { Write-Log -Level WARN -Message "Vários *.Api.csproj; usando o primeiro: $($candidates[0].FullName)" }
    return $candidates[0].FullName
}

function Invoke-BackendBuild {
    Write-Step "8/13  Build + testes do backend"
    $api = Resolve-ApiProject
    Write-Log -Level INFO -Message "Projeto da API: $api"
    $sln = Join-Path $script:BackendRoot 'Sistema.Gestao.Empresarial.sln'
    if ($DryRun) { Write-Log -Level DRY -Message "[dry-run] dotnet restore/build/test/publish"; return $api }

    # IMPORTANTE: o stdout do 'dotnet' precisa ir para o console/log, mas NÃO pode
    # virar valor de retorno da função — senão '$api = Invoke-BackendBuild' captura
    # todas as linhas do build/test junto do caminho do .csproj, e o
    # 'Publish-Backend' recebe um caminho de projeto inválido (MSB1009 / MAX_PATH).
    # '| Write-Host' consome a saída (fora do pipeline de retorno) e ainda a imprime.
    Push-Location $script:BackendRoot
    try {
        & dotnet restore $sln --locked-mode | Write-Host; if ($LASTEXITCODE) { throw "dotnet restore falhou." }
        & dotnet build $sln -c Release --no-restore | Write-Host; if ($LASTEXITCODE) { throw "dotnet build falhou." }
        if ($SkipTests) {
            Write-Log -Level WARN -Message "-SkipTests: pulando dotnet test."
        } else {
            & dotnet test $sln -c Release --no-build --nologo | Write-Host
            if ($LASTEXITCODE) { throw "dotnet test falhou. Publicação abortada." }
        }
    } finally { Pop-Location }
    return $api
}

function Publish-Backend {
    param([string]$ApiProject)
    Write-Step "9/13  Publicar API (deploy atômico com app_offline + rollback)"
    $target = Join-Path $IisRoot 'Api'
    # Área de stage com caminho CURTO: 'dotnet publish' gera árvores profundas
    # (runtimes\, wwwroot\, assets de pacotes) que estouram o MAX_PATH (260) se a
    # raiz for longa como %LOCALAPPDATA%\Temp. Fica ao lado do destino, no mesmo
    # volume (robocopy sem cópia entre discos), e é limpa ao final.
    $stage = Join-Path (Split-Path -Qualifier $IisRoot) ('\_sgepub\{0}' -f (Get-Date -Format 'HHmmss'))
    $backup = "$target.bak"
    if (Test-Path $stage) { Remove-Item $stage -Recurse -Force -ErrorAction SilentlyContinue }
    New-Item -ItemType Directory -Force -Path $stage | Out-Null

    if ($DryRun) {
        Write-Log -Level DRY -Message "[dry-run] dotnet publish '$ApiProject' -c Release -o '$stage'"
        Write-Log -Level DRY -Message "[dry-run] app_offline.htm em '$target', robocopy /MIR '$stage' -> '$target', remover app_offline"
        return
    }

    & dotnet publish $ApiProject -c Release --no-restore -o $stage /p:UseAppHost=true
    if ($LASTEXITCODE) { throw "dotnet publish falhou." }
    foreach ($must in 'Sistema.Gestao.Empresarial.Api.dll', 'web.config') {
        if (-not (Test-Path (Join-Path $stage $must))) { throw "Artefato inválido: '$must' ausente em $stage." }
    }
    # Nada de segredos/dev no pacote:
    Get-ChildItem $stage -Include '.env', '.env.*', '*.pdb' -Recurse -File -ErrorAction SilentlyContinue |
        Remove-Item -Force -ErrorAction SilentlyContinue

    New-Item -ItemType Directory -Force -Path $target | Out-Null
    $offline = Join-Path $target 'app_offline.htm'
    Set-Content -Path $offline -Value '<html><body>Publicando atualização...</body></html>' -Encoding UTF8
    Start-Sleep -Seconds 2   # deixa o ANCM drenar as requisições

    if (Test-Path $backup) { Remove-Item $backup -Recurse -Force }
    try {
        if ((Get-ChildItem $target -Force | Where-Object Name -ne 'app_offline.htm')) {
            Copy-Item $target $backup -Recurse -Force
        }
        $log = Join-Path $script:LogDir 'robocopy-api.log'
        & robocopy $stage $target /MIR /XF app_offline.htm /R:2 /W:2 /NFL /NDL /NP /LOG:$log | Out-Null
        if ($LASTEXITCODE -ge 8) { throw "robocopy falhou (código $LASTEXITCODE). Veja $log." }
        Write-Log -Level OK -Message "Arquivos da API atualizados em $target."
    } catch {
        Write-Log -Level ERROR -Message "Falha ao copiar publicação: $_. Tentando rollback..."
        if (Test-Path $backup) {
            & robocopy $backup $target /MIR /XF app_offline.htm /R:2 /W:2 /NFL /NDL /NP | Out-Null
            Write-Log -Level WARN -Message "Rollback concluído a partir de $backup."
        }
        throw
    } finally {
        Remove-Item $offline -Force -ErrorAction SilentlyContinue
        Remove-Item $stage -Recurse -Force -ErrorAction SilentlyContinue
    }
}

function Get-FrontendBuildOutput {
    param([string]$Root)
    # dist/<app>/browser (builder @angular/build:application). Descobre o mais recente.
    $dist = Join-Path $Root 'dist'
    if (-not (Test-Path $dist)) { throw "Saída de build do frontend não encontrada em $dist." }
    $browser = Get-ChildItem $dist -Recurse -Directory -Filter 'browser' -ErrorAction SilentlyContinue |
        Sort-Object LastWriteTime -Descending | Select-Object -First 1
    if ($browser) { return $browser.FullName }
    # Fallback: pasta com index.html
    $idx = Get-ChildItem $dist -Recurse -File -Filter 'index.html' -ErrorAction SilentlyContinue |
        Sort-Object LastWriteTime -Descending | Select-Object -First 1
    if ($idx) { return $idx.Directory.FullName }
    throw "Não encontrei index.html no build do frontend ($dist)."
}

function Publish-Frontend {
    Write-Step "10/13  Build + publicação do frontend Angular"
    if (-not (Test-Path $FrontendPath)) {
        throw "FrontendPath não existe: $FrontendPath. Informe -FrontendPath com o repositório do frontend (fonte da verdade)."
    }
    $pkg = Join-Path $FrontendPath 'package.json'
    if (-not (Test-Path $pkg)) { throw "package.json não encontrado em $FrontendPath." }
    $pkgJson = Get-Content $pkg -Raw | ConvertFrom-Json
    $buildScript = if ($pkgJson.scripts.PSObject.Properties.Name -contains 'build') { 'npm run build' } else { 'npx ng build --configuration production' }
    $target = Join-Path $IisRoot 'Frontend'

    if ($DryRun) {
        Write-Log -Level DRY -Message "[dry-run] (cd $FrontendPath) npm ci ; $buildScript"
        Write-Log -Level DRY -Message "[dry-run] robocopy /MIR <dist>/browser -> $target ; gerar web.config (SPA + proxy /api,/health -> http://127.0.0.1:$BackendPort)"
        return
    }

    Push-Location $FrontendPath
    try {
        & npm ci; if ($LASTEXITCODE) { throw "npm ci falhou." }
        if (-not $SkipTests -and ($pkgJson.scripts.PSObject.Properties.Name -contains 'test:ci')) {
            & npm run test:ci; if ($LASTEXITCODE) { throw "npm run test:ci falhou. A publicação foi interrompida." }
        }
        & cmd /c $buildScript; if ($LASTEXITCODE) { throw "Build do frontend falhou ($buildScript)." }
    } finally { Pop-Location }

    $out = Get-FrontendBuildOutput -Root $FrontendPath
    Write-Log -Level INFO -Message "Artefatos do frontend: $out"

    $backup = "$target.bak"
    New-Item -ItemType Directory -Force -Path $target | Out-Null
    if (Test-Path $backup) { Remove-Item $backup -Recurse -Force }
    if ((Get-ChildItem $target -Force -ErrorAction SilentlyContinue)) { Copy-Item $target $backup -Recurse -Force }
    try {
        $log = Join-Path $script:LogDir 'robocopy-frontend.log'
        & robocopy $out $target /MIR /XF web.config /R:2 /W:2 /NFL /NDL /NP /LOG:$log | Out-Null
        if ($LASTEXITCODE -ge 8) { throw "robocopy do frontend falhou ($LASTEXITCODE)." }
        Write-FrontendWebConfig -Path (Join-Path $target 'web.config')
        Write-FrontendRuntimeConfig -Path (Join-Path $target 'config.json')
        Write-Log -Level OK -Message "Frontend publicado em $target."
    } catch {
        if (Test-Path $backup) {
            & robocopy $backup $target /MIR /R:2 /W:2 /NFL /NDL /NP | Out-Null
            Write-Log -Level WARN -Message "Rollback do frontend a partir de $backup."
        }
        throw
    }
}

function Write-FrontendRuntimeConfig {
    param([string]$Path)
    $config = Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json
    $config.apiBaseUrl = if ($script:PublicMode) { "$PublicBackendOrigin/api" } else { '/api' }
    if ($script:PublicMode) {
        $config | Add-Member -NotePropertyName localApiBaseUrl -NotePropertyValue '/api' -Force
    } else {
        $config.PSObject.Properties.Remove('localApiBaseUrl')
    }
    $config | ConvertTo-Json | Set-Content -LiteralPath $Path -Encoding UTF8
}

function Write-FrontendWebConfig {
    param([string]$Path)
    # SPA fallback + reverse proxy same-origin de /api e /health para o site da API.
    # NÃO faz rewrite de /api para index.html (API e assets ficam fora do fallback).
    $connectSource = if ($script:PublicMode) { "'self' $PublicBackendOrigin" } else { "'self'" }
    $publicProxyBlock = if ($script:PublicMode) { @'
        <rule name="SGE public API uses its own hostname" stopProcessing="true">
          <match url="^(api|health)(/.*)?$" />
          <conditions><add input="{HTTP_HOST}" pattern="^localhost(:[0-9]+)?$" negate="true" /></conditions>
          <action type="CustomResponse" statusCode="404" statusReason="Not Found" statusDescription="Not Found" />
        </rule>
'@ } else { '' }
    $xml = @"
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <system.webServer>
    <httpProtocol>
      <customHeaders>
        <remove name="X-Powered-By" />
        <remove name="X-Content-Type-Options" />
        <add name="X-Content-Type-Options" value="nosniff" />
        <remove name="X-Frame-Options" />
        <add name="X-Frame-Options" value="DENY" />
        <remove name="Referrer-Policy" />
        <add name="Referrer-Policy" value="no-referrer" />
        <remove name="Permissions-Policy" />
        <add name="Permissions-Policy" value="camera=(), microphone=(), geolocation=()" />
        <remove name="Strict-Transport-Security" />
        <add name="Strict-Transport-Security" value="max-age=31536000; includeSubDomains" />
        <remove name="Content-Security-Policy" />
        <add name="Content-Security-Policy" value="default-src 'none'; script-src 'self'; style-src 'self' 'unsafe-inline'; img-src 'self' data:; font-src 'self'; connect-src $connectSource; frame-ancestors 'none'; base-uri 'self'; form-action 'self'; object-src 'none'" />
      </customHeaders>
    </httpProtocol>
    <directoryBrowse enabled="false" />
    <security>
      <requestFiltering removeServerHeader="true">
        <hiddenSegments>
          <remove segment="config.json" />
        </hiddenSegments>
      </requestFiltering>
    </security>
    <staticContent>
      <clientCache cacheControlMode="UseMaxAge" cacheControlMaxAge="365.00:00:00" />
    </staticContent>
    <rewrite>
      <rules>
$publicProxyBlock
        <!-- Proxy same-origin de /api e /health para o site da API (ARR).
             NÃO usa <serverVariables>: a seção system.webServer/rewrite/allowedServerVariables
             é travada em escopo de servidor e, referenciada num web.config de site,
             faz o módulo retornar "500 URL Rewrite Module Error". O ARR já encaminha
             X-Forwarded-For; o esquema é http em todo o caminho local. -->
        <rule name="SGE API proxy" stopProcessing="true">
          <match url="^(api|health)(/.*)?$" />
          <action type="Rewrite" url="http://127.0.0.1:$BackendPort/{R:0}" appendQueryString="true" logRewrittenUrl="false" />
        </rule>
        <rule name="SGE SPA fallback" stopProcessing="true">
          <match url=".*" />
          <conditions logicalGrouping="MatchAll">
            <add input="{REQUEST_FILENAME}" matchType="IsFile" negate="true" />
            <add input="{REQUEST_FILENAME}" matchType="IsDirectory" negate="true" />
            <add input="{REQUEST_URI}" pattern="^/(api|health)(/|$)" negate="true" />
          </conditions>
          <action type="Rewrite" url="/index.html" />
        </rule>
      </rules>
    </rewrite>
    <httpErrors errorMode="Custom" existingResponse="PassThrough" />
  </system.webServer>
  <location path="config.json">
    <system.webServer><staticContent><clientCache cacheControlMode="DisableCache" /></staticContent></system.webServer>
  </location>
  <location path="index.html">
    <system.webServer><staticContent><clientCache cacheControlMode="DisableCache" /></staticContent></system.webServer>
  </location>
</configuration>
"@
    if ($DryRun) { Write-Log -Level DRY -Message "[dry-run] gravar web.config do frontend em $Path"; return }
    Set-Content -Path $Path -Value $xml -Encoding UTF8
    Write-Log -Level OK -Message "web.config do frontend gerado (SPA + proxy /api,/health -> 127.0.0.1:$BackendPort)."
}

# ===========================================================================
#  AppPools + Sites
# ===========================================================================
function Get-ApiEnvironmentVariables {
    param([hashtable]$Env)
    $sql = "Server=127.0.0.1,$SqlServerLoopbackPort;Database=SistemaGestaoEmpresarial;User Id=$($Env['SGE_SQLSERVER_APP_USERNAME']);Password=$($Env['SGE_SQLSERVER_APP_PASSWORD']);Encrypt=True;TrustServerCertificate=True"
    $redis = "127.0.0.1:$RedisLoopbackPort,user=$($Env['SGE_REDIS_USERNAME']),password=$($Env['SGE_REDIS_PASSWORD']),abortConnect=false"
    $otelRatio = if ($Env.ContainsKey('SGE_OTEL_SAMPLING_RATIO') -and $Env['SGE_OTEL_SAMPLING_RATIO']) { $Env['SGE_OTEL_SAMPLING_RATIO'] } else { '0.1' }
    # Ordenado; os *segredos* nunca são impressos (Protect-Secret cobre o log).
    $variables = [ordered]@{
        'DOTNET_ENVIRONMENT'                = 'Production'
        'ASPNETCORE_ENVIRONMENT'           = 'Production'
        'ConnectionStrings__SqlServer'     = $sql
        'Redis__Configuration'             = $redis
        'Redis__InstanceName'              = 'sge'
        'RabbitMq__Host'                   = '127.0.0.1'
        'RabbitMq__Port'                   = "$RabbitMqLoopbackPort"
        'RabbitMq__VirtualHost'            = '/sge'
        'RabbitMq__Username'               = $Env['SGE_RABBITMQ_USERNAME']
        'RabbitMq__Password'               = $Env['SGE_RABBITMQ_PASSWORD']
        'Jwt__SigningKey'                  = $Env['SGE_JWT_SIGNING_KEY']
        'OpenTelemetry__Enabled'          = 'true'
        'OpenTelemetry__OtlpEndpoint'     = "http://127.0.0.1:$OtlpGrpcLoopbackPort"
        'OpenTelemetry__SamplingRatio'    = "$otelRatio"
        'ReverseProxy__Enabled'           = 'true'
        'ReverseProxy__ForwardLimit'      = '1'
        'ReverseProxy__KnownProxies__0'   = '127.0.0.1'
        'ReverseProxy__KnownProxies__1'   = '::1'
        'ReverseProxy__UseCloudflareHeaders' = "$($script:PublicMode)".ToLowerInvariant()
        'AllowedHosts'                     = 'localhost'
        'Swagger__Enabled'                 = 'false'
    }
    if ($script:PublicMode) {
        $variables['AllowedHosts'] = "localhost;$(([Uri]$PublicBackendOrigin).DnsSafeHost)"
        $variables['Cors__AllowedOrigins__0'] = $PublicFrontendOrigin
    }
    return $variables
}

function Set-AppPoolEnvironmentVariables {
    param([string]$Pool, [System.Collections.Specialized.OrderedDictionary]$Vars)
    $filterBase = "system.applicationHost/applicationPools/add[@name='$Pool']/environmentVariables"
    if ($DryRun) {
        foreach ($k in $Vars.Keys) { Write-Log -Level DRY -Message "[dry-run] AppPool '$Pool' env: $k=$(Protect-Secret ([string]$Vars[$k]))" }
        return
    }
    Clear-WebConfiguration -PSPath 'MACHINE/WEBROOT/APPHOST' -Filter $filterBase -ErrorAction SilentlyContinue
    foreach ($k in $Vars.Keys) {
        Add-WebConfigurationProperty -PSPath 'MACHINE/WEBROOT/APPHOST' -Filter $filterBase -Name '.' `
            -Value @{ name = $k; value = [string]$Vars[$k] }
        Write-Log -Level INFO -Message "AppPool '$Pool' env: $k=$(Protect-Secret ([string]$Vars[$k]))"
    }
    Write-Log -Level OK -Message "Variáveis de ambiente do AppPool '$Pool' aplicadas (armazenadas em applicationHost.config, fora do repositório)."
}

function New-OrUpdateAppPool {
    param([string]$Name, [switch]$AlwaysRunning)
    if ($DryRun) { Write-Log -Level DRY -Message "[dry-run] AppPool '$Name': No Managed Code, 64-bit, ApplicationPoolIdentity"; return }
    if (-not (Test-Path "IIS:\AppPools\$Name")) { New-WebAppPool -Name $Name | Out-Null; Write-Log -Level OK -Message "AppPool '$Name' criado." }
    else { Write-Log -Level OK -Message "AppPool '$Name' já existe (atualizando)." }
    Set-ItemProperty "IIS:\AppPools\$Name" -Name managedRuntimeVersion -Value ''            # No Managed Code
    Set-ItemProperty "IIS:\AppPools\$Name" -Name enable32BitAppOnWin64 -Value $false        # 64-bit
    Set-ItemProperty "IIS:\AppPools\$Name" -Name processModel.identityType -Value 'ApplicationPoolIdentity'
    Set-ItemProperty "IIS:\AppPools\$Name" -Name processModel.loadUserProfile -Value $true
    Set-ItemProperty "IIS:\AppPools\$Name" -Name startMode -Value ($(if ($AlwaysRunning) { 'AlwaysRunning' } else { 'OnDemand' }))
    Set-ItemProperty "IIS:\AppPools\$Name" -Name recycling.periodicRestart.time -Value ([TimeSpan]::Zero)
}

function Set-DeploymentAcl {
    param([string]$Folder, [string]$Pool)
    $identity = "IIS AppPool\$Pool"
    if ($DryRun) {
        Write-Log -Level DRY -Message "[dry-run] icacls '$Folder' /grant '${identity}:(OI)(CI)(RX)'  + Modify apenas em \logs"
        return
    }
    # Leitura/execução recursiva; SEM Full Control.
    & icacls $Folder /grant "${identity}:(OI)(CI)(RX)" /T /C /Q | Out-Null
    $logs = Join-Path $Folder 'logs'
    New-Item -ItemType Directory -Force -Path $logs | Out-Null
    & icacls $logs /grant "${identity}:(OI)(CI)(M)" /C /Q | Out-Null
    Write-Log -Level OK -Message "ACL mínima aplicada em '$Folder' para '$identity' (RX; Modify só em \logs)."
}

function Set-SiteLoopbackBindings {
    # Mantém EXATAMENTE dois bindings http: 127.0.0.1:<porta> e [::1]:<porta>.
    # O binding IPv6 é obrigatório porque 'localhost' resolve para ::1 ANTES de
    # 127.0.0.1 no Windows; sem ele, navegador/curl batem em ::1, o http.sys não
    # acha site para esse IP e responde 400 (e o cliente não faz fallback). Ambos
    # os endereços são de loopback (não roteáveis) => nada fica exposto na LAN.
    # Usa appcmd: 'New-WebBinding -IPAddress ::1' grava a string sem colchetes e
    # o site não inicia.
    param([string]$Name, [int]$Port)
    $appcmd = Join-Path $env:windir 'system32\inetsrv\appcmd.exe'
    $want = @("127.0.0.1:${Port}:", "[::1]:${Port}:")
    $current = @()
    foreach ($tok in ((& $appcmd list site $Name /text:bindings) -split ',')) {
        if ($tok -match '^\s*http/(.+?)\s*$') { $current += $Matches[1] }
    }
    foreach ($b in $current) {
        if ($b -notin $want) {
            & $appcmd set site $Name "/-bindings.[protocol='http',bindingInformation='$b']" | Out-Null
            Write-Log -Level INFO -Message "Site '$Name': binding http/$b removido (fora do conjunto loopback)."
        }
    }
    foreach ($b in $want) {
        if ($b -notin $current) {
            & $appcmd set site $Name "/+bindings.[protocol='http',bindingInformation='$b']" | Out-Null
            Write-Log -Level OK -Message "Site '$Name': binding http/$b adicionado."
        }
    }
}

function New-OrUpdateSite {
    param([string]$Name, [string]$PhysicalPath, [int]$Port, [string]$Pool)
    if ($DryRun) { Write-Log -Level DRY -Message "[dry-run] Site '$Name' -> $PhysicalPath  bindings http/127.0.0.1:$Port + http/[::1]:$Port  pool '$Pool'"; return }
    New-Item -ItemType Directory -Force -Path $PhysicalPath | Out-Null
    if (-not (Get-Website -Name $Name -ErrorAction SilentlyContinue)) {
        New-Website -Name $Name -PhysicalPath $PhysicalPath -ApplicationPool $Pool `
            -IPAddress '127.0.0.1' -Port $Port -Force | Out-Null
        Write-Log -Level OK -Message "Site '$Name' criado."
    } else {
        Set-ItemProperty "IIS:\Sites\$Name" -Name physicalPath -Value $PhysicalPath
        Set-ItemProperty "IIS:\Sites\$Name" -Name applicationPool -Value $Pool
        Write-Log -Level OK -Message "Site '$Name' atualizado."
    }
    Set-SiteLoopbackBindings -Name $Name -Port $Port
    Write-Log -Level OK -Message "Site '$Name' escutando em http://localhost:$Port (127.0.0.1 + [::1])."
}

function Set-ApiSiteHardening {
    param([string]$SiteName)
    if ($DryRun) { Write-Log -Level DRY -Message "[dry-run] hardening do site '$SiteName' (directory browse off, remove X-Powered-By, request limits)"; return }
    $ps = "MACHINE/WEBROOT/APPHOST/$SiteName"
    Set-WebConfigurationProperty -PSPath $ps -Filter 'system.webServer/directoryBrowse' -Name enabled -Value $false -ErrorAction SilentlyContinue
    Set-WebConfigurationProperty -PSPath $ps -Filter 'system.webServer/security/requestFiltering' -Name removeServerHeader -Value $true -ErrorAction SilentlyContinue
    # Alinhado ao KestrelSecurity.MaxRequestBodySizeBytes (1 MiB) do appsettings.json.
    Set-WebConfigurationProperty -PSPath $ps -Filter 'system.webServer/security/requestFiltering/requestLimits' -Name maxAllowedContentLength -Value 1048576 -ErrorAction SilentlyContinue
    try {
        Remove-WebConfigurationProperty -PSPath $ps -Filter 'system.webServer/httpProtocol/customHeaders' -Name '.' -AtElement @{name = 'X-Powered-By' } -ErrorAction SilentlyContinue
    } catch {}
    Write-Log -Level OK -Message "Hardening aplicado ao site '$SiteName'."
}

# ===========================================================================
#  Validação pós-deploy
# ===========================================================================
function Test-HttpOk {
    param([string]$Url, [int[]]$Accept = @(200), [string]$MustContain)
    try {
        $r = Invoke-WebRequest -Uri $Url -UseBasicParsing -TimeoutSec 20 -MaximumRedirection 0 -ErrorAction Stop
    } catch {
        $resp = $_.Exception.Response
        if ($resp -and ($Accept -contains [int]$resp.StatusCode)) { return $true }
        Write-Log -Level ERROR -Message "GET $Url -> ERRO: $($_.Exception.Message)"
        return $false
    }
    $code = [int]$r.StatusCode
    $okCode = $Accept -contains $code
    $okBody = (-not $MustContain) -or ($r.Content -match [regex]::Escape($MustContain))
    if ($okCode -and $okBody) { Write-Log -Level OK -Message "GET $Url -> $code"; return $true }
    Write-Log -Level ERROR -Message "GET $Url -> $code (esperado $($Accept -join '/'); corpo ok=$okBody)"
    return $false
}

function Test-Deployment {
    Write-Step "13/13  Health checks e validação"
    if ($DryRun) { Write-Log -Level DRY -Message "[dry-run] validação de health/API/frontend/proxy pulada"; return $true }
    Import-WebAdministration
    $ok = $true

    foreach ($pool in $script:ApiPoolName, $script:FrontendPoolName) {
        $st = (Get-WebAppPoolState -Name $pool -ErrorAction SilentlyContinue).Value
        if ($st -ne 'Started') { Write-Log -Level ERROR -Message "AppPool '$pool' está '$st' (esperado Started)."; $ok = $false }
        else { Write-Log -Level OK -Message "AppPool '$pool' Started." }
    }

    # 'localhost' (não 127.0.0.1): num site com binding de IP específico o http.sys
    # valida o Host header e devolve 400 para 'Host: 127.0.0.1'. O binding [::1]
    # garante que 'localhost' (que resolve para ::1 primeiro) funcione.
    $ok = (Test-HttpOk "http://localhost:$BackendPort/health/live") -and $ok
    $ok = (Test-HttpOk "http://localhost:$BackendPort/health/ready") -and $ok
    $ok = (Test-HttpOk "http://localhost:$FrontendPort/" -MustContain 'app-root') -and $ok
    $ok = (Test-HttpOk "http://localhost:$FrontendPort/login" -MustContain 'app-root') -and $ok   # SPA fallback
    $ok = (Test-HttpOk "http://localhost:$FrontendPort/config.json" -MustContain '"apiBaseUrl"') -and $ok
    $ok = (Test-HttpOk "http://localhost:$FrontendPort/health/ready") -and $ok                    # proxy same-origin
    # Swagger NÃO deve responder em Production:
    if (Test-HttpOk "http://localhost:$BackendPort/swagger/index.html" -Accept @(404, 401)) {
        Write-Log -Level OK -Message "Swagger não exposto (Production)."
    } else {
        Write-Log -Level WARN -Message "Swagger respondeu em /swagger — verifique Swagger:Enabled/ambiente."
    }
    return $ok
}

# ===========================================================================
#  Ações
# ===========================================================================
function Invoke-Publish {
    Assert-PublicOrigins
    $envMap = Read-DotEnv
    Assert-RequiredSecrets -EnvMap $envMap

    Test-WslReady
    Test-DockerReady
    Start-IisInfrastructurePorts
    Test-DatabaseSchema

    $freshIis = -not (Test-IisInstalled)
    Install-Iis
    if (-not $DryRun) { Import-WebAdministration }
    Install-AspNetCoreHostingBundle -IisWasFreshlyInstalled:$freshIis
    Install-ReverseProxyModules

    $api = Invoke-BackendBuild

    Write-Step "11/13  AppPools + ACL"
    New-OrUpdateAppPool -Name $script:ApiPoolName -AlwaysRunning
    New-OrUpdateAppPool -Name $script:FrontendPoolName
    Set-AppPoolEnvironmentVariables -Pool $script:ApiPoolName -Vars (Get-ApiEnvironmentVariables -Env $envMap)

    # Para a API antes de trocar arquivos (deploy atômico).
    if (-not $DryRun -and (Test-Path "IIS:\AppPools\$($script:ApiPoolName)")) {
        if ((Get-WebAppPoolState -Name $script:ApiPoolName).Value -eq 'Started') {
            Stop-WebAppPool -Name $script:ApiPoolName; Start-Sleep 2
            Write-Log -Level INFO -Message "AppPool da API parado para publicação."
        }
    }

    Publish-Backend -ApiProject $api
    Publish-Frontend

    Write-Step "12/13  Sites IIS"
    New-OrUpdateSite -Name $script:ApiSiteName -PhysicalPath (Join-Path $IisRoot 'Api') -Port $BackendPort -Pool $script:ApiPoolName
    New-OrUpdateSite -Name $script:FrontendSiteName -PhysicalPath (Join-Path $IisRoot 'Frontend') -Port $FrontendPort -Pool $script:FrontendPoolName
    Set-ApiSiteHardening -SiteName $script:ApiSiteName
    Set-DeploymentAcl -Folder (Join-Path $IisRoot 'Api') -Pool $script:ApiPoolName
    Set-DeploymentAcl -Folder (Join-Path $IisRoot 'Frontend') -Pool $script:FrontendPoolName

    if (-not $DryRun) {
        Start-WebAppPool -Name $script:ApiPoolName -ErrorAction SilentlyContinue
        Start-WebAppPool -Name $script:FrontendPoolName -ErrorAction SilentlyContinue
        Start-Website -Name $script:ApiSiteName -ErrorAction SilentlyContinue
        Start-Website -Name $script:FrontendSiteName -ErrorAction SilentlyContinue
        Start-Sleep -Seconds 4
    }

    $healthy = Test-Deployment
    Write-Summary
    if (-not $healthy -and -not $DryRun) { throw "Publicação concluída com FALHAS nos health checks (veja acima e o log)." }
}

function Invoke-StartStop {
    param([ValidateSet('Start', 'Stop')] [string]$Mode)
    Import-WebAdministration
    foreach ($site in $script:FrontendSiteName, $script:ApiSiteName) {
        if (Get-Website -Name $site -ErrorAction SilentlyContinue) {
            if ($Mode -eq 'Start') { Start-Website -Name $site } else { Stop-Website -Name $site -ErrorAction SilentlyContinue }
        }
    }
    foreach ($pool in $script:FrontendPoolName, $script:ApiPoolName) {
        if (Test-Path "IIS:\AppPools\$pool") {
            if ($Mode -eq 'Start') { Start-WebAppPool -Name $pool } else { Stop-WebAppPool -Name $pool -ErrorAction SilentlyContinue }
        }
    }
    Write-Log -Level OK -Message "Sites/AppPools do SGE: $Mode concluído. (Docker/WSL e ambiente de dev NÃO foram tocados.)"
    if ($Mode -eq 'Start') { Start-Sleep 3; Invoke-Status }
}

function Invoke-Status {
    Import-WebAdministration
    "`n  Componente             Endereço                          Estado" | Write-Host
    "  ---------------------  --------------------------------  --------" | Write-Host
    foreach ($row in @(
            @{ c = 'Frontend IIS'; u = "http://localhost:$FrontendPort"; site = $script:FrontendSiteName; pool = $script:FrontendPoolName },
            @{ c = 'Backend IIS'; u = "http://localhost:$BackendPort"; site = $script:ApiSiteName; pool = $script:ApiPoolName }
        )) {
        $ss = (Get-Website -Name $row.site -ErrorAction SilentlyContinue).State
        if (-not $ss) { $ss = 'ausente' }
        $ps = (Get-WebAppPoolState -Name $row.pool -ErrorAction SilentlyContinue).Value
        if (-not $ps) { $ps = 'ausente' }
        "  {0,-21}  {1,-32}  site={2} pool={3}" -f $row.c, $row.u, $ss, $ps | Write-Host
    }
    Write-Host ''
    foreach ($p in @(
            @{ n = 'SQL Server (loopback)'; port = $SqlServerLoopbackPort },
            @{ n = 'Redis (loopback)'; port = $RedisLoopbackPort },
            @{ n = 'RabbitMQ AMQP (loopback)'; port = $RabbitMqLoopbackPort },
            @{ n = 'OTLP gRPC (loopback)'; port = $OtlpGrpcLoopbackPort },
            @{ n = 'OTLP HTTP (loopback)'; port = $OtlpHttpLoopbackPort }
        )) {
        $t = Test-NetConnection -ComputerName 127.0.0.1 -Port $p.port -WarningAction SilentlyContinue
        "  {0,-25} 127.0.0.1:{1,-6} {2}" -f $p.n, $p.port, ($(if ($t.TcpTestSucceeded) { 'OK' } else { 'sem resposta' })) | Write-Host
    }
    Write-Host ''
    try {
        $live = Invoke-WebRequest "http://localhost:$BackendPort/health/live" -UseBasicParsing -TimeoutSec 8
        Write-Host "  API /health/live -> $([int]$live.StatusCode)"
    } catch { Write-Host "  API /health/live -> indisponível" }
}

function Invoke-Remove {
    Import-WebAdministration
    Write-Log -Level WARN -Message "Removendo APENAS a configuração IIS do SGE. Docker/WSL, banco, volumes e ambiente de dev permanecem intactos."
    if ($DryRun) { Write-Log -Level DRY -Message "[dry-run] remover sites, pools, env vars e (opcional) pastas em $IisRoot"; return }
    foreach ($site in $script:FrontendSiteName, $script:ApiSiteName) {
        if (Get-Website -Name $site -ErrorAction SilentlyContinue) { Remove-Website -Name $site; Write-Log -Level OK -Message "Site '$site' removido." }
    }
    foreach ($pool in $script:FrontendPoolName, $script:ApiPoolName) {
        if (Test-Path "IIS:\AppPools\$pool") {
            Stop-WebAppPool -Name $pool -ErrorAction SilentlyContinue
            Remove-WebAppPool -Name $pool
            Write-Log -Level OK -Message "AppPool '$pool' removido."
        }
    }
    Write-Log -Level INFO -Message "Arquivos em '$IisRoot' foram mantidos. Apague manualmente se desejar: Remove-Item '$IisRoot' -Recurse -Force"
    Write-Log -Level INFO -Message "Para retirar as portas de loopback da infra: docker compose -f docker-compose.yml -f docker-compose.override.yml up -d --no-deps sqlserver redis rabbitmq otel-collector"
    Write-Log -Level INFO -Message "ARR/URL Rewrite e o Hosting Bundle NÃO são desinstalados (podem ser usados por outros sites). Remova pelo 'Programas e Recursos' se quiser."
}

function Write-Summary {
    Write-Host ''
    Write-Log -Level STEP -Message 'RESUMO'
    "  Componente        Endereço                     Observação"                     | Write-Host
    "  ----------------  ---------------------------  ------------------------------" | Write-Host
    "  Frontend IIS      http://localhost:$FrontendPort            SPA Angular (mesma origem)"    | Write-Host
    "  Backend  IIS      http://localhost:$BackendPort            API .NET 10 (só loopback)"     | Write-Host
    "  -> /api,/health   http://localhost:$FrontendPort/api        proxy p/ 127.0.0.1:$BackendPort"     | Write-Host
    "  SQL Server        127.0.0.1:$SqlServerLoopbackPort              MESMO container do Docker/WSL"  | Write-Host
    "  Redis             127.0.0.1:$RedisLoopbackPort              MESMO container do Docker/WSL"  | Write-Host
    "  RabbitMQ (AMQP)   127.0.0.1:$RabbitMqLoopbackPort              MESMO broker do Docker/WSL"     | Write-Host
    "  OpenTelemetry     127.0.0.1:$OtlpGrpcLoopbackPort              MESMO Collector do Docker/WSL"  | Write-Host
    Write-Host ''
    "  Log desta execução: $($script:LogFile)"                                        | Write-Host
    "  Republicar : .\scripts\publish-local-iis.ps1"                                  | Write-Host
    "  Status     : .\scripts\publish-local-iis.ps1 -Action Status"                   | Write-Host
    "  Parar      : .\scripts\publish-local-iis.ps1 -Action Stop"                     | Write-Host
    "  Iniciar    : .\scripts\publish-local-iis.ps1 -Action Start"                    | Write-Host
    "  Remover    : .\scripts\publish-local-iis.ps1 -Action Remove"                   | Write-Host
    if ($DryRun) { Write-Host ''; Write-Log -Level DRY -Message 'DRY-RUN: nada foi instalado, criado, publicado ou reiniciado.' }
}

# ===========================================================================
#  Main
# ===========================================================================
try {
    Write-Log -Level STEP -Message "publish-local-iis.ps1  Action=$Action  DryRun=$DryRun"
    Write-Log -Level INFO -Message "Backend=$($script:BackendRoot)  Frontend=$FrontendPath  IisRoot=$IisRoot"
    Write-Log -Level INFO -Message "Portas -> Frontend=$FrontendPort Backend=$BackendPort | infra loopback: SQL=$SqlServerLoopbackPort Redis=$RedisLoopbackPort RabbitMQ=$RabbitMqLoopbackPort OTLP=$OtlpGrpcLoopbackPort/$OtlpHttpLoopbackPort"

    Assert-Administrator

    switch ($Action) {
        'Publish' {
            # Conflitos de porta ANTES de qualquer alteração (sem escolher outra em silêncio).
            Assert-PortFreeForUs -Port $FrontendPort -Purpose 'FrontendPort' -OwnedBy @($script:FrontendSiteName)
            Assert-PortFreeForUs -Port $BackendPort  -Purpose 'BackendPort'  -OwnedBy @($script:ApiSiteName)
            foreach ($ip in @(
                    @{ p = $SqlServerLoopbackPort; n = 'SqlServerLoopbackPort' },
                    @{ p = $RedisLoopbackPort; n = 'RedisLoopbackPort' },
                    @{ p = $RabbitMqLoopbackPort; n = 'RabbitMqLoopbackPort' },
                    @{ p = $OtlpGrpcLoopbackPort; n = 'OtlpGrpcLoopbackPort' },
                    @{ p = $OtlpHttpLoopbackPort; n = 'OtlpHttpLoopbackPort' }
                )) {
                # Estas PODEM já estar publicadas por uma execução anterior do overlay (Docker). Isso é OK.
                $owner = Get-PortOwnerDescription -Port $ip.p
                if ($owner -match 'Docker/WSL') { Write-Log -Level OK -Message "Porta $($ip.p) já publicada pelo overlay (Docker/WSL). OK." }
                elseif ($owner -notmatch 'não identificada') { throw "Porta $($ip.p) ($($ip.n)) ocupada por: $owner. Informe outra porta." }
                else { Write-Log -Level OK -Message "Porta $($ip.p) livre ($($ip.n))." }
            }
            Invoke-Publish
        }
        'Start' { Invoke-StartStop -Mode Start }
        'Stop' { Invoke-StartStop -Mode Stop }
        'Status' { Invoke-Status }
        'Remove' { Invoke-Remove }
    }
    Write-Log -Level OK -Message "Concluído: Action=$Action."
    exit 0
} catch {
    Write-Log -Level ERROR -Message ("FALHA: {0}" -f (Protect-Secret ($_.Exception.Message)))
    Write-Log -Level ERROR -Message ("Em: {0}:{1}" -f $_.InvocationInfo.ScriptName, $_.InvocationInfo.ScriptLineNumber)
    Write-Log -Level INFO  -Message "Log completo: $($script:LogFile)"
    exit 1
}
