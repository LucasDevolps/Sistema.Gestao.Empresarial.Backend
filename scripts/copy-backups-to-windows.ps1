#Requires -Version 7
<#
.SYNOPSIS
  Copia o backup validado mais recente do SQL Server para um ou mais
  armazenamentos Windows (redundancia entre discos) e aplica a politica de
  retencao por destino.

.DESCRIPTION
  Fluxo:
    1. (Opcional -RunBackup) executa scripts/backup-and-verify-sqlserver.sh no WSL,
       que gera o .bak com CHECKSUM, RESTORE VERIFYONLY, SHA-256 e DBCC CHECKDB.
    2. Seleciona o par .bak + .sha256 mais recente em -BackupSource.
    3. Revalida o SHA-256 na origem; aborta se divergir.
    4. Para CADA destino em -Destinations:
       a. Garante o diretorio com ACL restritiva (SYSTEM, Administradores e o
          usuario atual; sem herança; sem grupo Users).
       b. Copia .bak e .sha256 e revalida o SHA-256 no destino.
       c. Somente apos uma copia nova validada, remove as antigas mantendo as
          -RetainCount mais recentes.
       d. Grava um registro JSONL de observabilidade (sem segredos).
    5. Sucesso se PELO MENOS UM destino recebeu uma copia validada. Se algum
       destino falhar, o script termina com erro apos concluir os demais
       (a redundancia e preservada, mas a falha fica visivel).

  Destinos padrao (redundancia C: + D:), usados quando -Destinations e a variavel
  SGE_BACKUP_WINDOWS_DESTINATION nao sao informados:
    - C:\ProgramData\SistemaGestaoEmpresarial\Backups\SQLServer
    - D:\Backups\SistemaGestaoEmpresarial\SQLServer
  Destinos em discos ausentes/nao fixos sao ignorados com aviso (nao derrubam a
  execucao) desde que ao menos um destino valido reste.

  Nao ha senhas neste script. Para destino remoto, aponte para um
  compartilhamento ja autenticado/criptografado (SMB 3 com criptografia ou
  equivalente); este script nao trafega credenciais.

.PARAMETER BackupSource
  Diretorio com os .bak e .sha256. Padrao: <repo>\artifacts\backups.

.PARAMETER Destinations
  Um ou mais diretorios de destino (redundancia). Padrao: os dois destinos C:/D:
  acima, ou a lista em SGE_BACKUP_WINDOWS_DESTINATION separada por ';'.

.PARAMETER Destination
  Compatibilidade: um unico destino. Se informado, e adicionado a -Destinations.

.PARAMETER RetainCount
  Quantidade de copias validas a manter POR destino. Padrao: 2 (minimo 1).

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

.EXAMPLE
  pwsh ./scripts/copy-backups-to-windows.ps1 -RunBackup -Destinations 'C:\ProgramData\SGE\bak','E:\bak'
#>
[CmdletBinding()]
param(
  [string]$BackupSource,
  [string[]]$Destinations,
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

$DefaultDestinations = @(
  'C:\ProgramData\SistemaGestaoEmpresarial\Backups\SQLServer'
  'D:\Backups\SistemaGestaoEmpresarial\SQLServer'
)

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

function Resolve-Destinations {
  $list = [System.Collections.Generic.List[string]]::new()
  foreach ($d in $Destinations) { if ($d) { $list.Add($d.Trim()) } }
  if ($Destination) { $list.Add($Destination.Trim()) }
  if ($list.Count -eq 0 -and $env:SGE_BACKUP_WINDOWS_DESTINATION) {
    foreach ($d in ($env:SGE_BACKUP_WINDOWS_DESTINATION -split ';')) { if ($d.Trim()) { $list.Add($d.Trim()) } }
  }
  if ($list.Count -eq 0) { foreach ($d in $DefaultDestinations) { $list.Add($d) } }
  # Remove duplicados preservando a ordem.
  $seen = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
  $result = foreach ($d in $list) { if ($seen.Add($d)) { $d } }
  return @($result)
}

# Retorna $true quando o caminho e utilizavel: unidade existente e, para caminhos
# com letra de unidade, disco local fixo (DriveType 3) ou removivel/rede ja montada.
function Test-DestinationUsable {
  param([string]$Path)
  $qualifier = Split-Path -Qualifier $Path -ErrorAction SilentlyContinue
  if (-not $qualifier) { return $true }  # UNC ou relativo: deixa o Copy-Item decidir.
  $letter = $qualifier.TrimEnd(':')
  $disk = Get-CimInstance Win32_LogicalDisk -Filter "DeviceID='${letter}:'" -ErrorAction SilentlyContinue
  # 2 = removivel, 3 = fixo, 4 = rede. Recusamos apenas o que nao existe.
  return [bool]$disk
}

function Set-RestrictiveAcl {
  param([string]$Path)
  # Sem herança; apenas SYSTEM, Administradores e o usuario atual com controle
  # total. Usa SIDs (sempre resolviveis) em vez de nomes.
  $sids = [ordered]@{}
  $sids['S-1-5-18']      = $null  # NT AUTHORITY\SYSTEM
  $sids['S-1-5-32-544']  = $null  # BUILTIN\Administrators
  $me = ([System.Security.Principal.WindowsIdentity]::GetCurrent()).User.Value
  $sids[$me] = $null

  $acl = Get-Acl -Path $Path
  $acl.SetAccessRuleProtection($true, $false)
  # Remove regras explicitas herdadas/pre-existentes.
  foreach ($rule in @($acl.Access | Where-Object { -not $_.IsInherited })) {
    [void]$acl.RemoveAccessRule($rule)
  }
  foreach ($sid in $sids.Keys) {
    $rule = New-Object System.Security.AccessControl.FileSystemAccessRule(
      (New-Object System.Security.Principal.SecurityIdentifier($sid)),
      [System.Security.AccessControl.FileSystemRights]::FullControl,
      'ContainerInherit,ObjectInherit', 'None', 'Allow')
    $acl.AddAccessRule($rule)
  }
  Set-Acl -Path $Path -AclObject $acl
}

# Copia + valida + aplica retencao em UM destino. Lanca em falha.
function Copy-ToDestination {
  param(
    [string]$Dest,
    [System.IO.FileInfo]$SourceFile,
    [string]$ExpectedHash
  )
  $sourceBak = $SourceFile.FullName
  $sourceSha = "$sourceBak.sha256"

  if (-not (Test-Path $Dest)) { New-Item -ItemType Directory -Force -Path $Dest | Out-Null }
  Set-RestrictiveAcl -Path $Dest

  $destBak = Join-Path $Dest $SourceFile.Name
  $destSha = "$destBak.sha256"
  Copy-Item -Path $sourceBak -Destination $destBak -Force
  Copy-Item -Path $sourceSha -Destination $destSha -Force

  $destActual = Get-Sha256Hex $destBak
  if ($destActual -ne $ExpectedHash) {
    Remove-Item -Path $destBak, $destSha -Force -ErrorAction SilentlyContinue
    throw "SHA-256 nao confere apos copiar para $Dest (esperado $ExpectedHash, obtido $destActual). Copia removida."
  }
  $destSize = (Get-Item $destBak).Length

  $validCopies = @(
    Get-ChildItem -Path $Dest -Filter '*.bak' -File |
      Where-Object { Test-Path "$($_.FullName).sha256" } |
      Sort-Object LastWriteTimeUtc -Descending
  )
  $removed = @()
  if ($validCopies.Count -gt $RetainCount) {
    foreach ($old in ($validCopies | Select-Object -Skip $RetainCount)) {
      Remove-Item -Path $old.FullName, "$($old.FullName).sha256" -Force
      $removed += $old.Name
    }
  }

  return [pscustomobject]@{
    Destination     = $Dest
    DestinationPath = $destBak
    SizeBytes       = $destSize
    CopiesRetained  = [Math]::Min($validCopies.Count, $RetainCount)
    CopiesRemoved   = $removed
  }
}

function Register-WeeklyTask {
  $pwsh = (Get-Process -Id $PID).Path
  if (-not $pwsh) { $pwsh = 'pwsh.exe' }
  $scriptPath = Join-Path $PSScriptRoot 'copy-backups-to-windows.ps1'
  $taskName = 'SGE - Copia semanal de backup SQL Server'
  $arguments = "-NoProfile -NonInteractive -File `"$scriptPath`" -RunBackup -RetainCount $RetainCount"
  # Só fixa destinos na tarefa quando informados explicitamente; caso contrário a
  # tarefa usa a resolução padrão do script (redundância C: + D:), o que a mantém
  # compatível mesmo se o script for atualizado depois.
  $explicit = @()
  foreach ($d in $Destinations) { if ($d) { $explicit += $d.Trim() } }
  if ($Destination) { $explicit += $Destination.Trim() }
  if ($explicit.Count -gt 0) {
    $destArg = ($explicit | ForEach-Object { '"{0}"' -f $_ }) -join ','
    $arguments += " -Destinations $destArg"
  }
  $destList = Resolve-Destinations

  $action = New-ScheduledTaskAction -Execute $pwsh -Argument $arguments -WorkingDirectory $RepoRoot
  $trigger = New-ScheduledTaskTrigger -Weekly -DaysOfWeek $ScheduleDay -At $ScheduleTime
  $principal = New-ScheduledTaskPrincipal -UserId ([System.Security.Principal.WindowsIdentity]::GetCurrent().Name) -LogonType S4U -RunLevel Highest
  $settings = New-ScheduledTaskSettingsSet -StartWhenAvailable -DontStopOnIdleEnd -MultipleInstances IgnoreNew -ExecutionTimeLimit (New-TimeSpan -Hours 2)

  Register-ScheduledTask -TaskName $taskName -Action $action -Trigger $trigger -Principal $principal -Settings $settings -Force | Out-Null
  Write-Host "Tarefa agendada '$taskName' registrada: toda $ScheduleDay as $ScheduleTime." -ForegroundColor Green
  Write-Host "Destinos: $($destList -join ' | ')" -ForegroundColor DarkGray
  Write-Host "Comando: $pwsh $arguments" -ForegroundColor DarkGray
}

if ($Register) {
  Register-WeeklyTask
  return
}

if (-not $BackupSource) { $BackupSource = Join-Path $RepoRoot 'artifacts\backups' }
$targets = Resolve-Destinations

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
  Write-Host "== Backup selecionado: $($latest.Name) ==" -ForegroundColor Cyan

  $expected = Read-ExpectedHash "$sourceBak.sha256"
  $actual = Get-Sha256Hex $sourceBak
  if ($actual -ne $expected) {
    throw "SHA-256 da origem nao confere para $($latest.Name) (esperado $expected, obtido $actual)."
  }
  Write-Host '   hash da origem validado.' -ForegroundColor DarkGray

  # Filtra destinos cujo disco nao existe (ex.: D: ausente), desde que reste um.
  $usable = @($targets | Where-Object { Test-DestinationUsable $_ })
  $skipped = @($targets | Where-Object { $_ -notin $usable })
  foreach ($s in $skipped) { Write-Warning "Destino ignorado (unidade ausente): $s" }
  if ($usable.Count -eq 0) { throw "Nenhum destino utilizavel entre: $($targets -join ', ')" }

  $ok = @()
  $failed = @()
  foreach ($dest in $usable) {
    try {
      Write-Host "== Destino: $dest ==" -ForegroundColor Cyan
      $r = Copy-ToDestination -Dest $dest -SourceFile $latest -ExpectedHash $expected
      $ok += $r
      Write-Host "   copia validada em $($r.DestinationPath) (retidas: $($r.CopiesRetained), removidas: $($r.CopiesRemoved.Count))" -ForegroundColor DarkGray
      Write-ObservabilityRecord @{
        result           = 'success'
        backup_file      = $latest.Name
        source_path      = $sourceBak
        destination_path = $r.DestinationPath
        size_bytes       = $r.SizeBytes
        sha256           = $expected
        retain_count     = $RetainCount
        copies_retained  = $r.CopiesRetained
        copies_removed   = $r.CopiesRemoved
      }
    }
    catch {
      $failed += [pscustomobject]@{ Destination = $dest; Error = $_.Exception.Message }
      Write-Warning "Falha no destino $dest : $($_.Exception.Message)"
      Write-ObservabilityRecord @{
        result           = 'failure'
        backup_file      = $latest.Name
        source_path      = $sourceBak
        destination_path = $dest
        sha256           = $expected
        error            = $_.Exception.Message
      }
    }
  }

  $summaryResult = 'failure'
  if ($ok.Count -gt 0) { $summaryResult = if ($failed.Count -gt 0) { 'partial' } else { 'success' } }
  Write-ObservabilityRecord @{
    result                = $summaryResult
    event_kind            = 'summary'
    backup_file           = $latest.Name
    destinations_ok       = @($ok | ForEach-Object { $_.Destination })
    destinations_failed   = @($failed | ForEach-Object { $_.Destination })
    skipped_missing_drive = $skipped
  }

  if ($ok.Count -eq 0) {
    throw "Todos os destinos falharam: $($failed.Destination -join ', ')"
  }

  Write-Host ''
  Write-Host "Backup replicado em $($ok.Count) destino(s): $($ok.Destination -join ' | ')" -ForegroundColor Green
  Write-Host "Registro de observabilidade: $LogFile" -ForegroundColor Green

  if ($failed.Count -gt 0) {
    throw "Redundancia parcial: $($failed.Count) destino(s) falharam ($($failed.Destination -join ', ')). Copia mantida nos destinos OK."
  }
}
catch {
  Write-ObservabilityRecord @{
    result     = 'failure'
    event_kind = 'run'
    error      = $_.Exception.Message
    source_dir = $BackupSource
  }
  throw
}
