#Requires -Version 7
<#
.SYNOPSIS
  Sobe todo o ambiente de desenvolvimento (backend em Docker via WSL + frontend Angular)
  em um unico comando. Idempotente: em "warm start" apenas religa o que estiver parado.

.DESCRIPTION
  Backend  : docker compose (WSL/Ubuntu) -> SQL Server, Redis, RabbitMQ, OTEL, API, Worker, Nginx
  Migracoes: aplicadas automaticamente na primeira vez (volume sql-data vazio)
  Frontend : ng serve com o Node do Windows (WSL nao tem Node), publicado em http://localhost:4200
  Proxy    : /api e /health do frontend -> https://localhost:8443 (Nginx do backend)

.PARAMETER SkipFrontend
  Sobe apenas o backend.

.PARAMETER Bootstrap
  Apos subir, cria o administrador inicial se ainda nao existir. Requer o arquivo de
  senha em SGE_BOOTSTRAP_PASSWORD_FILE (ver .env). Seguro rodar sempre: sai sem alterar
  nada (codigo 3) se qualquer usuario ja existir.

.EXAMPLE
  pwsh ./scripts/dev-up.ps1
#>
[CmdletBinding()]
param(
  [switch]$SkipFrontend,
  [switch]$Bootstrap
)

$ErrorActionPreference = 'Stop'
$Distro       = 'Ubuntu'
$BackendWsl   = '/mnt/c/Users/lucas/source/repos/Sistema.Gestao.Empresarial.Backend'
$FrontendWin  = 'c:\Users\lucas\source\repos\Sistema.Gestao.Empresarial.Frontend'
$FrontendPort = 4200
$LogDir       = Join-Path $env:TEMP 'sge-dev'
$null = New-Item -ItemType Directory -Force -Path $LogDir

function Invoke-Wsl([string]$BashCommand) {
  wsl.exe -d $Distro -- bash -lc $BashCommand
  if ($LASTEXITCODE -ne 0) { throw "WSL command failed ($LASTEXITCODE): $BashCommand" }
}
function Invoke-WslQuiet([string]$BashCommand) {
  $out = wsl.exe -d $Distro -- bash -lc $BashCommand
  return @{ Code = $LASTEXITCODE; Out = ($out -join "`n") }
}

Write-Host '== 0/5  Sessao keepalive do WSL ==' -ForegroundColor Cyan
# Quando o ultimo processo wsl.exe termina, o WSL derruba a distro: systemd para
# docker/cron/snapd e os containers caem (parada graciosa, exit 0). Mantemos UMA
# sessao interativa aberta (janela minimizada) para a distro nao ser desligada.
# Feche a janela "SGE keepalive" para permitir que o ambiente ocioso desligue.
$ka = Get-CimInstance Win32_Process -Filter "Name = 'wsl.exe'" -ErrorAction SilentlyContinue |
  Where-Object { $_.CommandLine -match 'SGE-KEEPALIVE' }
if ($ka) {
  Write-Host '   ja rodando.' -ForegroundColor DarkGray
} else {
  Start-Process -FilePath 'wsl.exe' -WindowStyle Minimized -ArgumentList @(
    '-d', $Distro, '--exec', '/bin/sh', '-c', 'echo SGE-KEEPALIVE; exec sleep infinity'
  )
  Write-Host '   janela minimizada iniciada (feche-a para liberar a distro).' -ForegroundColor DarkGray
  Start-Sleep 2
}

Write-Host '== 1/5  SQL Server ==' -ForegroundColor Cyan
# Sem --wait aqui: sqlserver-init e one-shot (exit 0) e algumas versoes do compose
# fazem o --wait retornar erro por causa disso. Subimos e aguardamos o healthcheck.
Invoke-Wsl "cd $BackendWsl && docker compose up -d sqlserver sqlserver-init"
$sqlReady = $false
foreach ($i in 1..40) {
  $st = Invoke-WslQuiet "cd $BackendWsl && docker compose ps sqlserver --format '{{.Status}}'"
  if ($st.Out -match 'healthy') { $sqlReady = $true; break }
  Start-Sleep 3
}
if (-not $sqlReady) { throw "SQL Server nao ficou healthy a tempo." }
Write-Host '   healthy.' -ForegroundColor DarkGray

Write-Host '== 2/5  Migracoes (aplica apenas se o banco estiver sem schema) ==' -ForegroundColor Cyan
$check = Invoke-WslQuiet "cd $BackendWsl && bash ./scripts/_db-has-schema.sh"
if ($check.Code -eq 0) {
  Write-Host '   schema presente, pulando.' -ForegroundColor DarkGray
} else {
  Write-Host "   $($check.Out.Trim()) -> aplicando migracoes..." -ForegroundColor Yellow
  Invoke-Wsl "cd $BackendWsl && bash ./scripts/apply-migrations-docker.sh"
}

Write-Host '== 3/5  API, Worker, Nginx ==' -ForegroundColor Cyan
Invoke-Wsl "cd $BackendWsl && docker compose up -d --wait"

if ($Bootstrap) {
  Write-Host '== 3b   Bootstrap do administrador inicial (no-op se ja existir) ==' -ForegroundColor Cyan
  $b = Invoke-WslQuiet "cd $BackendWsl && bash ./scripts/bootstrap-initial-admin-docker.sh"
  Write-Host $b.Out
  if     ($b.Code -eq 0) { Write-Host '   administrador criado.' -ForegroundColor Green }
  elseif ($b.Code -eq 3) { Write-Host '   ja existia um usuario, nada alterado.' -ForegroundColor DarkGray }
  else                   { Write-Warning "   bootstrap retornou codigo $($b.Code)" }
}

Write-Host '== 4/5  Frontend (ng serve, Node do Windows) ==' -ForegroundColor Cyan
$listening = Get-NetTCPConnection -LocalPort $FrontendPort -State Listen -ErrorAction SilentlyContinue
if ($SkipFrontend) {
  Write-Host '   -SkipFrontend, ignorado.' -ForegroundColor DarkGray
} elseif ($listening) {
  Write-Host "   ja ha algo escutando na porta $FrontendPort, ignorado." -ForegroundColor DarkGray
} else {
  if (-not (Test-Path (Join-Path $FrontendWin 'node_modules'))) {
    Write-Host '   npm ci...' -ForegroundColor Yellow
    Push-Location $FrontendWin; npm ci; Pop-Location
  }
  $log = Join-Path $LogDir 'ng-serve.log'
  Start-Process -FilePath 'pwsh' -WindowStyle Hidden -ArgumentList @(
    '-NoProfile','-Command',
    "Set-Location '$FrontendWin'; npm start *>&1 | Tee-Object -FilePath '$log'"
  )
  Write-Host "   iniciado em background, log: $log" -ForegroundColor DarkGray
  for ($i = 0; $i -lt 60; $i++) {
    Start-Sleep 2
    if (Get-NetTCPConnection -LocalPort $FrontendPort -State Listen -ErrorAction SilentlyContinue) { break }
  }
}

Write-Host '== 5/5  Status ==' -ForegroundColor Cyan
Invoke-Wsl "cd $BackendWsl && docker compose ps --format 'table {{.Service}}\t{{.Status}}'"
Write-Host ''
Write-Host 'Frontend : http://localhost:4200'         -ForegroundColor Green
Write-Host 'API/Nginx: https://localhost:8443  (cert self-signed; /swagger, /nginx-health)' -ForegroundColor Green
Write-Host 'Parar    : pwsh ./scripts/dev-down.ps1'   -ForegroundColor Green
