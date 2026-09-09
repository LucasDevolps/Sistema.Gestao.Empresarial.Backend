#Requires -Version 7
<#
.SYNOPSIS
  Copia o backup validado mais recente do SQL Server para um armazenamento
  Windows fora do host de containers e aplica a politica de retencao de 2 copias.

.DESCRIPTION
  Fluxo:
    1. (Opcional -RunBackup) executa scripts/backup-and-verify-sqlserver.sh no WSL,
       que gera o .bak com CHECKSUM, RESTORE VERIFYONLY, SHA-256 e DBCC CHECKDB.
    2. Seleciona o par .bak + .sha256 mais recente em -BackupSource.
    3. Revalida o SHA-256 na origem; aborta se divergir.
    4. Garante o diretorio de destino com ACL restritiva (SYSTEM, Administradores
       e o usuario atual; sem herança; sem grupo Users).
    5. Copia .bak e .sha256 e revalida o SHA-256 no destino.
    6. Somente apos uma copia nova validada, remove as copias antigas mantendo as
       -RetainCount mais recentes.
    7. Grava um registro JSONL de observabilidade (sem segredos).

  Destino automatico: usa D:\Backups\SistemaGestaoEmpresarial\SQLServer quando o
  volume D: existir e for um disco fixo diferente do disco do repositorio; caso
  contrario C:\ProgramData\SistemaGestaoEmpresarial\Backups\SQLServer.

  Nao ha senhas neste script. Para destino remoto, aponte -Destination para um
  compartilhamento ja autenticado/criptografado (SMB 3 com criptografia ou
  equivalente); este script nao trafega credenciais.

.PARAMETER BackupSource
  Diretorio com os .bak e .sha256. Padrao: <repo>\artifacts\backups.

.PARAMETER Destination
  Diretorio de destino. Padrao: resolvido automaticamente (ver acima) ou pela
  variavel de ambiente SGE_BACKUP_WINDOWS_DESTINATION.

.PARAMETER RetainCount
  Quantidade de copias validas a manter no destino. Padrao: 2 (minimo 1).

.PARAMETER RunBackup
  Executa o script de backup no WSL antes de copiar.

.PARAMETER Distro
  Distribuicao WSL usada com -RunBackup. Padrao: Ubuntu.

.PARAMETER Register
  Registra/atualiza a tarefa semanal no Windows Task Scheduler e sai.

.PARAMETER ScheduleDay
  Dia da semana para -Register. Padrao: Sunday.

.PARAMETER ScheduleTime
  Horario (HH:mm) para -Register. Padrao: 03:00.

.EXAMPLE
  pwsh ./scripts/copy-backups-to-windows.ps1 -RunBackup

.EXAMPLE
  pwsh ./scripts/copy-backups-to-windows.ps1 -Register
#>
[CmdletBinding()]
param(
  [string]$BackupSource,
  [string]$Destination,
  [ValidateRange(1, 50)][int]$RetainCount = 2,
  [switch]$RunBackup,
  [string]$Distro = 'Ubuntu',
  [switch]$Register,
  [ValidateSet('Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday', 'Saturday', 'Sunday')]
  [string]$ScheduleDay = 'Sunday',
  [ValidatePattern('^([01]\d|2[0-3]):[0-5]\d$')]
  [string]$ScheduleTime = '03:00'
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$RepoRoot = Split-Path -Parent $PSScriptRoot
$LogFile = Join-Path $RepoRoot 'logs\backup-windows-copy.jsonl'

function Write-ObservabilityRecord {
  param([hashtable]$Fields)
  $record = [ordered]@{ timestamp = (Get-Date).ToUniversalTime().ToString('yyyy-MM-ddTHH:mm:ssZ'); event = 'sqlserver_backup_windows_copy' }
  foreach ($key in $Fields.Keys) { $record[$key] = $Fields[$key] }
  $dir = Split-Path -Parent $LogFile
  if (-not (Test-Path $dir)) { New-Item -ItemType Directory -Force -Path $dir | Out-Null }
  ($record | ConvertTo-Json -Compress -Depth 4) | Add-Content -Path $LogFile -Encoding utf8
}

function Get-Sha256Hex {
  param([string]$Path)
  (Get-FileHash -Path $Path -Algorithm SHA256).Hash.ToLowerInvariant()
}

# Le o hash esperado do arquivo .sha256 (formato "hash  nome", estilo sha256sum).
function Read-ExpectedHash {
  param([string]$Sha256File)
  $line = (Get-Content -Path $Sha256File -TotalCount 1).Trim()
  $token = ($line -split '\s+')[0]
  if ($token -notmatch '^[0-9a-fA-F]{64}$') { throw "Arquivo de hash invalido: $Sha256File" }
  return $token.ToLowerInvariant()
}

function Resolve-DefaultDestination {
  if ($env:SGE_BACKUP_WINDOWS_DESTINATION) { return $env:SGE_BACKUP_WINDOWS_DESTINATION }
  $preferred = 'D:\Backups\SistemaGestaoEmpresarial\SQLServer'
  $fallback = 'C:\ProgramData\SistemaGestaoEmpresarial\Backups\SQLServer'
  $repoDrive = (Split-Path -Qualifier $RepoRoot).TrimEnd(':')
  $dDrive = Get-CimInstance Win32_LogicalDisk -Filter "DeviceID='D:'" -ErrorAction SilentlyContinue
  # DriveType 3 = disco local fixo. Preferimos diversidade de disco fisico.
  if ($dDrive -and $dDrive.DriveType -eq 3 -and $repoDrive -ne 'D') { return $preferred }
  return $fallback
}

function Set-RestrictiveAcl {
  param([string]$Path)
  # Sem herança; apenas SYSTEM, Administradores e o usuario atual com controle total.
  $acl = New-Object System.Security.AccessControl.DirectorySecurity
  $acl.SetAccessRuleProtection($true, $false)
  $identities = @(
    'NT AUTHORITY\SYSTEM',
    'BUILTIN\Administrators',
    [System.Security.Principal.WindowsIdentity]::GetCurrent().Name
  ) | Select-Object -Unique
  foreach ($id in $identities) {
    $rule = New-Object System.Security.AccessControl.FileSystemAccessRule(
      $id, 'FullControl',
      'ContainerInherit,ObjectInherit', 'None', 'Allow')
    $acl.AddAccessRule($rule)
  }
  Set-Acl -Path $Path -AclObject $acl
}

function Register-WeeklyTask {
  $pwsh = (Get-Process -Id $PID).Path
  if (-not $pwsh) { $pwsh = 'pwsh.exe' }
  $scriptPath = Join-Path $PSScriptRoot 'copy-backups-to-windows.ps1'
  $taskName = 'SGE - Copia semanal de backup SQL Server'
  $arguments = "-NoProfile -NonInteractive -File `"$scriptPath`" -RunBackup"
  if ($Destination) { $arguments += " -Destination `"$Destination`"" }
  $arguments += " -RetainCount $RetainCount"

  $action = New-ScheduledTaskAction -Execute $pwsh -Argument $arguments -WorkingDirectory $RepoRoot
  $trigger = New-ScheduledTaskTrigger -Weekly -DaysOfWeek $ScheduleDay -At $ScheduleTime
  $principal = New-ScheduledTaskPrincipal -UserId ([System.Security.Principal.WindowsIdentity]::GetCurrent().Name) -LogonType S4U -RunLevel Highest
  $settings = New-ScheduledTaskSettingsSet -StartWhenAvailable -DontStopOnIdleEnd -MultipleInstances IgnoreNew -ExecutionTimeLimit (New-TimeSpan -Hours 2)

  Register-ScheduledTask -TaskName $taskName -Action $action -Trigger $trigger -Principal $principal -Settings $settings -Force | Out-Null
  Write-Host "Tarefa agendada '$taskName' registrada: toda $ScheduleDay as $ScheduleTime." -ForegroundColor Green
  Write-Host "Comando: $pwsh $arguments" -ForegroundColor DarkGray
}

if ($Register) {
  Register-WeeklyTask
  return
}

if (-not $BackupSource) { $BackupSource = Join-Path $RepoRoot 'artifacts\backups' }
if (-not $Destination) { $Destination = Resolve-DefaultDestination }

try {
  if ($RunBackup) {
    Write-Host '== Executando backup no WSL ==' -ForegroundColor Cyan
    $wslRepo = (wsl.exe -d $Distro -- wslpath -a "$RepoRoot").Trim()
    if ($LASTEXITCODE -ne 0 -or -not $wslRepo) { throw "Nao foi possivel resolver o caminho WSL de $RepoRoot" }
    wsl.exe -d $Distro -- bash -lc "cd '$wslRepo' && bash scripts/backup-and-verify-sqlserver.sh"
    if ($LASTEXITCODE -ne 0) { throw "scripts/backup-and-verify-sqlserver.sh falhou (codigo $LASTEXITCODE)." }
  }

  if (-not (Test-Path $BackupSource)) { throw "Diretorio de origem nao encontrado: $BackupSource" }

  $latest = Get-ChildItem -Path $BackupSource -Filter '*.bak' -File |
    Where-Object { Test-Path "$($_.FullName).sha256" } |
    Sort-Object LastWriteTimeUtc -Descending |
    Select-Object -First 1
  if (-not $latest) { throw "Nenhum backup .bak com .sha256 correspondente em $BackupSource" }

  $sourceBak = $latest.FullName
  $sourceSha = "$sourceBak.sha256"
  Write-Host "== Backup selecionado: $($latest.Name) ==" -ForegroundColor Cyan

  $expected = Read-ExpectedHash $sourceSha
  $actual = Get-Sha256Hex $sourceBak
  if ($actual -ne $expected) {
    throw "SHA-256 da origem nao confere para $($latest.Name) (esperado $expected, obtido $actual)."
  }
  Write-Host '   hash da origem validado.' -ForegroundColor DarkGray

  if (-not (Test-Path $Destination)) {
    New-Item -ItemType Directory -Force -Path $Destination | Out-Null
  }
  Set-RestrictiveAcl -Path $Destination
  Write-Host "== Destino: $Destination (ACL restritiva aplicada) ==" -ForegroundColor Cyan

  $destBak = Join-Path $Destination $latest.Name
  $destSha = "$destBak.sha256"
  if (Test-Path $destBak) {
    Write-Host '   copia ja existe no destino; revalidando.' -ForegroundColor DarkGray
  }
  Copy-Item -Path $sourceBak -Destination $destBak -Force
  Copy-Item -Path $sourceSha -Destination $destSha -Force

  $destActual = Get-Sha256Hex $destBak
  if ($destActual -ne $expected) {
    Remove-Item -Path $destBak, $destSha -Force -ErrorAction SilentlyContinue
    throw "SHA-256 do destino nao confere apos a copia (esperado $expected, obtido $destActual). Copia removida."
  }
  $destSize = (Get-Item $destBak).Length
  Write-Host '   copia no destino validada.' -ForegroundColor DarkGray

  # Retencao: so remove antigos DEPOIS de uma copia nova validada existir.
  $validCopies = Get-ChildItem -Path $Destination -Filter '*.bak' -File |
    Where-Object { Test-Path "$($_.FullName).sha256" } |
    Sort-Object LastWriteTimeUtc -Descending
  $removed = @()
  if ($validCopies.Count -gt $RetainCount) {
    foreach ($old in $validCopies | Select-Object -Skip $RetainCount) {
      Remove-Item -Path $old.FullName, "$($old.FullName).sha256" -Force
      $removed += $old.Name
      Write-Host "   removido (retencao): $($old.Name)" -ForegroundColor DarkGray
    }
  }

  Write-ObservabilityRecord @{
    result           = 'success'
    backup_file      = $latest.Name
    source_path      = $sourceBak
    destination_path = $destBak
    size_bytes       = $destSize
    sha256           = $expected
    retain_count     = $RetainCount
    copies_retained  = [Math]::Min($validCopies.Count, $RetainCount)
    copies_removed   = $removed
  }

  Write-Host ''
  Write-Host "Backup copiado e validado em: $destBak" -ForegroundColor Green
  Write-Host "Registro de observabilidade: $LogFile" -ForegroundColor Green
}
catch {
  Write-ObservabilityRecord @{
    result           = 'failure'
    error            = $_.Exception.Message
    source_dir       = $BackupSource
    destination_path = $Destination
  }
  throw
}
