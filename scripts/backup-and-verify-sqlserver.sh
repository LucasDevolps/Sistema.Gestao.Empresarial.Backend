#!/usr/bin/env bash
set -euo pipefail

project_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
backup_directory="${SGE_BACKUP_DIRECTORY:-${project_root}/artifacts/backups}"
backup_log_file="${SGE_BACKUP_LOG_FILE:-${project_root}/logs/backup-sqlserver.jsonl}"
database="SistemaGestaoEmpresarial"
stamp="$(date -u +%Y%m%dT%H%M%SZ)"
backup_name="${database}-${stamp}.bak"
container_backup="/var/opt/mssql/backup/${backup_name}"
validation_database="SGE_RestoreValidation_${stamp}"
container_id="$(docker compose --project-directory "${project_root}" ps -q sqlserver)"

# Observabilidade: uma linha JSON por execução, sem segredos. Campos: horário,
# resultado, caminho, tamanho, hash e etapas de validação.
backup_sha256=""
backup_size=""
backup_completed=0
log_event() {
  local result="$1" stage="$2"
  mkdir -p "$(dirname "${backup_log_file}")"
  printf '{"timestamp":"%s","event":"sqlserver_backup","database":"%s","result":"%s","stage":"%s","backup_file":"%s","size_bytes":"%s","sha256":"%s","verifyonly":"CHECKSUM","dbcc_checkdb":true}\n' \
    "$(date -u +%Y-%m-%dT%H:%M:%SZ)" "${database}" "${result}" "${stage}" \
    "${backup_directory}/${backup_name}" "${backup_size}" "${backup_sha256}" \
    >> "${backup_log_file}"
}
current_stage="inicialização"
# Registra falha para qualquer saída não bem-sucedida (erro, exit explícito ou sinal).
trap '[[ "${backup_completed}" -eq 1 ]] || log_event "failure" "${current_stage}"' EXIT

if [[ -z "${container_id}" ]]; then
  echo "SQL Server do projeto não está em execução." >&2
  exit 1
fi

mkdir -p "${backup_directory}"

if [[ -e "${backup_directory}/${backup_name}" || -e "${backup_directory}/${backup_name}.sha256" ]]; then
  echo "Já existe um backup com o nome ${backup_name}; execução abortada para não sobrescrever." >&2
  exit 1
fi

current_stage="BACKUP + RESTORE VERIFYONLY"
docker exec --env "BACKUP_PATH=${container_backup}" "${container_id}" bash -euc '
  mkdir -p /var/opt/mssql/backup
  /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "$MSSQL_SA_PASSWORD" -C -b \
    -Q "BACKUP DATABASE [SistemaGestaoEmpresarial] TO DISK = N'"'"'${BACKUP_PATH}'"'"' WITH COPY_ONLY, CHECKSUM, INIT"
  /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "$MSSQL_SA_PASSWORD" -C -b \
    -Q "RESTORE VERIFYONLY FROM DISK = N'"'"'${BACKUP_PATH}'"'"' WITH CHECKSUM"
'

current_stage="cópia e hash SHA-256"
docker cp "${container_id}:${container_backup}" "${backup_directory}/${backup_name}" >/dev/null
sha256sum "${backup_directory}/${backup_name}" > "${backup_directory}/${backup_name}.sha256"
backup_sha256="$(cut -d' ' -f1 < "${backup_directory}/${backup_name}.sha256")"
backup_size="$(wc -c < "${backup_directory}/${backup_name}" | tr -d '[:space:]')"

data_logical="$(docker exec "${container_id}" bash -euc '
  /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "$MSSQL_SA_PASSWORD" -C -h -1 -W \
    -Q "SET NOCOUNT ON; SELECT name FROM sys.master_files WHERE database_id = DB_ID(N'"'"'SistemaGestaoEmpresarial'"'"') AND type = 0"
')"
log_logical="$(docker exec "${container_id}" bash -euc '
  /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "$MSSQL_SA_PASSWORD" -C -h -1 -W \
    -Q "SET NOCOUNT ON; SELECT name FROM sys.master_files WHERE database_id = DB_ID(N'"'"'SistemaGestaoEmpresarial'"'"') AND type = 1"
')"

if [[ ! "${data_logical}" =~ ^[A-Za-z0-9_.-]+$ || ! "${log_logical}" =~ ^[A-Za-z0-9_.-]+$ ]]; then
  echo "Nomes lógicos inesperados no backup; restauração cancelada." >&2
  exit 1
fi

current_stage="RESTORE de validação + DBCC CHECKDB"
docker exec \
  --env "BACKUP_PATH=${container_backup}" \
  --env "VALIDATION_DATABASE=${validation_database}" \
  --env "DATA_LOGICAL=${data_logical}" \
  --env "LOG_LOGICAL=${log_logical}" \
  "${container_id}" bash -euc '
  sqlcmd=(/opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "$MSSQL_SA_PASSWORD" -C -b)
  existing="$("${sqlcmd[@]}" -h -1 -W -Q "SET NOCOUNT ON; SELECT COUNT(*) FROM sys.databases WHERE name = N'"'"'${VALIDATION_DATABASE}'"'"'")"
  if [[ "${existing}" != "0" ]]; then
    echo "Banco temporário de validação já existe; operação recusada." >&2
    exit 1
  fi

  cleanup() {
    "${sqlcmd[@]}" -Q "IF DB_ID(N'"'"'${VALIDATION_DATABASE}'"'"') IS NOT NULL BEGIN ALTER DATABASE [${VALIDATION_DATABASE}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [${VALIDATION_DATABASE}]; END" >/dev/null || true
    rm -f "/var/opt/mssql/data/${VALIDATION_DATABASE}.mdf" "/var/opt/mssql/data/${VALIDATION_DATABASE}_log.ldf"
  }
  trap cleanup EXIT

  "${sqlcmd[@]}" -Q "RESTORE DATABASE [${VALIDATION_DATABASE}] FROM DISK = N'"'"'${BACKUP_PATH}'"'"' WITH CHECKSUM, MOVE N'"'"'${DATA_LOGICAL}'"'"' TO N'"'"'/var/opt/mssql/data/${VALIDATION_DATABASE}.mdf'"'"', MOVE N'"'"'${LOG_LOGICAL}'"'"' TO N'"'"'/var/opt/mssql/data/${VALIDATION_DATABASE}_log.ldf'"'"'"
  "${sqlcmd[@]}" -Q "DBCC CHECKDB ([${VALIDATION_DATABASE}]) WITH NO_INFOMSGS, DATA_PURITY"
'

current_stage="concluído"
log_event "success" "${current_stage}"
backup_completed=1

echo "Backup criado, verificado, restaurado e validado com DBCC CHECKDB: ${backup_directory}/${backup_name}"
echo "Registro de observabilidade: ${backup_log_file}"
