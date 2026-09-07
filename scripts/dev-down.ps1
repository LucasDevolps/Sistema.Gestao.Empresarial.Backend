#Requires -Version 7
<#
.SYNOPSIS
  Para o ambiente de desenvolvimento iniciado por dev-up.ps1.

.DESCRIPTION
  Encerra o ng serve (porta 4200) e para os containers do backend.
  Por padrao usa "docker compose stop" (containers e volumes preservados; religa rapido).
  O processo keepalive da VM do WSL e mantido (a VM segue viva para o proximo dev-up);
  use -Full para encerra-lo tambem.

.PARAMETER Down
  Usa "docker compose down" em vez de "stop" (remove containers e redes).
  Os volumes nomeados (sql-data etc.) sao mantidos de qualquer forma.

.PARAMETER Volumes
  Com -Down, tambem remove os volumes -> apaga o banco. Pede confirmacao.

.PARAMETER Full
  Tambem encerra o keepalive 'sge-wsl-keepalive'. A VM do WSL passa a poder
  desligar por ociosidade; o proximo dev-up faz cold boot (mais lento).
#>
[CmdletBinding()]
param(
  [switch]$Down,
  [switch]$Volumes,
  [switch]$Full
)

$ErrorActionPreference = 'Stop'
$Distro     = 'Ubuntu'
$BackendWsl = '/mnt/c/Users/lucas/source/repos/Sistema.Gestao.Empresarial.Backend'
$FrontendPort = 4200

Write-Host '== Frontend (ng serve) ==' -ForegroundColor Cyan
$conns = Get-NetTCPConnection -LocalPort $FrontendPort -State Listen -ErrorAction SilentlyContinue
if ($conns) {
  $conns.OwningProcess | Select-Object -Unique | ForEach-Object {
    try { Stop-Process -Id $_ -Force -ErrorAction Stop; Write-Host "   PID $_ encerrado." }
    catch { Write-Warning "   nao consegui encerrar PID $_" }
  }
} else { Write-Host '   nada escutando na 4200.' -ForegroundColor DarkGray }
# limpa processos node orfaos do ng serve deste frontend
Get-CimInstance Win32_Process -Filter "Name = 'node.exe'" -ErrorAction SilentlyContinue |
  Where-Object { $_.CommandLine -match 'Sistema\.Gestao\.Empresarial\.Frontend' } |
  ForEach-Object { Stop-Process -Id $_.ProcessId -Force -ErrorAction SilentlyContinue }

Write-Host '== Backend (docker compose) ==' -ForegroundColor Cyan
if ($Down) {
  if ($Volumes) {
    $ans = Read-Host 'Remover TAMBEM os volumes (apaga o banco)? digite "sim"'
    if ($ans -eq 'sim') { wsl.exe -d $Distro -- bash -lc "cd $BackendWsl && docker compose down -v" }
    else { Write-Host '   cancelado, mantendo volumes.'; wsl.exe -d $Distro -- bash -lc "cd $BackendWsl && docker compose down" }
  } else {
    wsl.exe -d $Distro -- bash -lc "cd $BackendWsl && docker compose down"
  }
} else {
  wsl.exe -d $Distro -- bash -lc "cd $BackendWsl && docker compose stop"
}

if ($Full) {
  Write-Host '== Sessao keepalive do WSL ==' -ForegroundColor Cyan
  $ka = Get-CimInstance Win32_Process -Filter "Name = 'wsl.exe'" -ErrorAction SilentlyContinue |
    Where-Object { $_.CommandLine -match 'SGE-KEEPALIVE' }
  if ($ka) { $ka | ForEach-Object { Stop-Process -Id $_.ProcessId -Force -ErrorAction SilentlyContinue }; Write-Host '   encerrada.' }
  else { Write-Host '   nao estava rodando.' -ForegroundColor DarkGray }
}

Write-Host ''
Write-Host 'Ambiente parado. Religar: pwsh ./scripts/dev-up.ps1' -ForegroundColor Green
