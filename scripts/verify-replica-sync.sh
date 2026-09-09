#!/usr/bin/env bash
# Relata a saúde da replicação Always On entre `sqlserver` e `sqlserver-replica`.
# Sai com 0 somente quando o banco SistemaGestaoEmpresarial está SYNCHRONIZED e
# HEALTHY na réplica; caso contrário sai com 2 (dessincronizado) ou 1 (erro).
#
# Uso: wsl bash scripts/verify-replica-sync.sh [--json]
set -euo pipefail

project_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
environment_file="${project_root}/.env"
database="SistemaGestaoEmpresarial"
availability_group="SGE_AG"
emit_json=0
[[ "${1:-}" == "--json" ]] && emit_json=1

[[ -f "${environment_file}" ]] || { echo "Arquivo .env não encontrado." >&2; exit 1; }
set -a; # shellcheck disable=SC1090
source "${environment_file}"; set +a
: "${SGE_SQLSERVER_SA_PASSWORD:?Defina SGE_SQLSERVER_SA_PASSWORD no .env}"

compose() { docker compose --project-directory "${project_root}" -f "${project_root}/docker-compose.yml" -f "${project_root}/docker-compose.replica.yml" "$@"; }
primary_id="$(compose ps -q sqlserver || true)"
[[ -n "${primary_id}" ]] || { echo "Instância 'sqlserver' não está em execução." >&2; exit 1; }

query() {
  docker exec -i --env "TSQL=$1" "${primary_id}" bash -euc \
    '/opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "$MSSQL_SA_PASSWORD" -C -b -h -1 -W -s "|" -Q "SET NOCOUNT ON; $TSQL"'
}

ag_present="$(query "SELECT COUNT(*) FROM sys.availability_groups WHERE name = '${availability_group}'" | tr -d '[:space:]')"
if [[ "${ag_present}" != "1" ]]; then
  echo "Grupo de disponibilidade ${availability_group} não encontrado. Rode scripts/configure-availability-group.sh." >&2
  exit 1
fi

row="$(query "
  SELECT
    ar.replica_server_name,
    ars.role_desc,
    drs.synchronization_state_desc,
    drs.synchronization_health_desc,
    ISNULL(CONVERT(varchar(30), drs.last_hardened_lsn), 'n/d'),
    ISNULL(CONVERT(varchar(20), drs.log_send_queue_size), 'n/d'),
    ISNULL(CONVERT(varchar(20), drs.redo_queue_size), 'n/d'),
    ISNULL(CONVERT(varchar(30), drs.last_commit_time, 126), 'n/d')
  FROM sys.dm_hadr_database_replica_states drs
  JOIN sys.availability_replicas ar ON ar.replica_id = drs.replica_id
  JOIN sys.dm_hadr_availability_replica_states ars ON ars.replica_id = drs.replica_id
  JOIN sys.databases d ON d.database_id = drs.database_id
  WHERE d.name = '${database}' AND ars.role_desc = 'SECONDARY';")"

if [[ -z "${row//[[:space:]]/}" ]]; then
  echo "Nenhuma réplica secundária reporta o banco ${database}. Seeding pode não ter iniciado." >&2
  exit 2
fi

IFS='|' read -r server role sync_state sync_health hardened_lsn send_queue redo_queue last_commit <<<"${row}"
server="${server// /}"; role="${role// /}"; sync_state="${sync_state// /}"; sync_health="${sync_health// /}"

if [[ "${emit_json}" == "1" ]]; then
  printf '{"timestamp":"%s","availability_group":"%s","database":"%s","secondary_replica":"%s","role":"%s","synchronization_state":"%s","synchronization_health":"%s","last_hardened_lsn":"%s","log_send_queue_kb":"%s","redo_queue_kb":"%s","last_commit_time":"%s"}\n' \
    "$(date -u +%Y-%m-%dT%H:%M:%SZ)" "${availability_group}" "${database}" "${server}" "${role}" \
    "${sync_state}" "${sync_health}" "${hardened_lsn// /}" "${send_queue// /}" "${redo_queue// /}" "${last_commit// /}"
else
  echo "Grupo de disponibilidade : ${availability_group}"
  echo "Banco                    : ${database}"
  echo "Réplica secundária       : ${server} (${role})"
  echo "Estado de sincronização  : ${sync_state}"
  echo "Saúde da sincronização   : ${sync_health}"
  echo "Último LSN endurecido    : ${hardened_lsn// /}"
  echo "Fila de envio de log (KB): ${send_queue// /}"
  echo "Fila de redo (KB)        : ${redo_queue// /}"
  echo "Último commit aplicado   : ${last_commit// /}"
fi

if [[ "${sync_state}" == "SYNCHRONIZED" && "${sync_health}" == "HEALTHY" ]]; then
  exit 0
fi
echo "Réplica NÃO está sincronizada e saudável." >&2
exit 2
