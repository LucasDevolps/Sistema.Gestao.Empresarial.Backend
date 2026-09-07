#Requires -Version 7
<#
.SYNOPSIS
  Mantem a VM do WSL (distro Ubuntu) sempre viva.

.DESCRIPTION
  A VM do WSL desliga quando o ultimo processo wsl.exe termina; nesta maquina
  algo (ex.: extensao Docker do VS Code) chama wsl.exe periodicamente, cada
  chamada acorda e depois deixa a distro ociosa -> systemd re-init em loop ->
  os containers do stack SGE ficam reiniciando -> o proxy do 'ng serve' devolve
  500/503 de forma intermitente.

  Este guardiao roda em background e garante que exista SEMPRE um
  'wsl.exe ... sleep infinity' (marcado SGE-KEEPALIVE) segurando a distro.
  Se essa sessao cair (acontece durante um re-init), ele sobe outra na hora.

  Parar: feche a janela, ou
    Get-CimInstance Win32_Process -Filter "Name='pwsh.exe'" |
      ? { $_.CommandLine -match 'wsl-keepalive-guardian' } | % { Stop-Process -Id $_.ProcessId }
#>
[CmdletBinding()]
param([string]$Distro = 'Ubuntu', [int]$CheckSeconds = 5)

$ErrorActionPreference = 'Continue'

function Test-KeepaliveAlive {
  $p = Get-CimInstance Win32_Process -Filter "Name = 'wsl.exe'" -ErrorAction SilentlyContinue |
    Where-Object { $_.CommandLine -match 'SGE-KEEPALIVE' }
  return [bool]$p
}

function Start-Keepalive {
  Start-Process -FilePath 'wsl.exe' -WindowStyle Hidden -ArgumentList @(
    '-d', $Distro, '--exec', '/bin/sh', '-c', 'echo SGE-KEEPALIVE; exec sleep infinity'
  )
}

Write-Host "[guardian] iniciado para distro '$Distro' (checa a cada ${CheckSeconds}s)"
while ($true) {
  if (-not (Test-KeepaliveAlive)) {
    Start-Keepalive
    Write-Host "[guardian] $(Get-Date -Format o)  keepalive (re)iniciado"
    Start-Sleep -Seconds 2
  }
  Start-Sleep -Seconds $CheckSeconds
}
