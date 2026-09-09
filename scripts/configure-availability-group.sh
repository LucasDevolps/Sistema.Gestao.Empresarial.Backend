#!/usr/bin/env bash
# Configura o grupo de disponibilidade Always On "read-scale" (CLUSTER_TYPE = NONE)
# entre a instância primária (`sqlserver`) e a réplica de espera
# (`sqlserver-replica`) do Compose. Replicação SÍNCRONA e failover MANUAL.
#
# Pré-requisitos:
#   - Pilha no ar com o overlay da réplica:
#       docker compose -f docker-compose.yml -f docker-compose.replica.yml up -d
#   - .env com SGE_SQLSERVER_SA_PASSWORD, SGE_SQLSERVER_REPLICA_SA_PASSWORD e
#     SGE_AG_CERTIFICATE_PASSWORD definidos.
#
# O script é idempotente: reexecutar não recria objetos já existentes. Ele nunca
# imprime senhas e remove os arquivos temporários de certificado ao final.
set -euo pipefail

project_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
environment_file="${project_root}/.env"
availability_group="SGE_AG"
database="SistemaGestaoEmpresarial"
endpoint_port=5022
primary_service="sqlserver"
replica_service="sqlserver-replica"

if [[ ! -f "${environment_file}" ]]; then
  echo "Arquivo .env não encontrado. Crie-o a partir de .env.example." >&2
  exit 1
fi

set -a
# shellcheck disable=SC1090
source "${environment_file}"
set +a

: "${SGE_SQLSERVER_SA_PASSWORD:?Defina SGE_SQLSERVER_SA_PASSWORD no .env}"
: "${SGE_SQLSERVER_REPLICA_SA_PASSWORD:?Defina SGE_SQLSERVER_REPLICA_SA_PASSWORD no .env}"
: "${SGE_AG_CERTIFICATE_PASSWORD:?Defina SGE_AG_CERTIFICATE_PASSWORD no .env}"

compose() { docker compose --project-directory "${project_root}" -f "${project_root}/docker-compose.yml" -f "${project_root}/docker-compose.replica.yml" "$@"; }

primary_id="$(compose ps -q "${primary_service}" || true)"
replica_id="$(compose ps -q "${replica_service}" || true)"
if [[ -z "${primary_id}" || -z "${replica_id}" ]]; then
  echo "Instâncias '${primary_service}' e/ou '${replica_service}' não estão em execução." >&2
  echo "Suba a pilha com o overlay docker-compose.replica.yml antes de configurar o AG." >&2
  exit 1
fi

work_dir="$(mktemp -d)"
cleanup() { rm -rf "${work_dir}"; }
trap cleanup EXIT

# Executa um lote T-SQL em uma instância. $1 = container id, $2 = descrição, $3 = T-SQL.
run_sql() {
  local container_id="$1" label="$2" tsql="$3"
  docker exec -i --env "TSQL=${tsql}" "${container_id}" bash -euc '
    /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "$MSSQL_SA_PASSWORD" -C -b -l 15 -Q "$TSQL"
  ' >/dev/null
  echo "  ok: ${label}"
}

query_scalar() {
  local container_id="$1" tsql="$2" raw
  raw="$(docker exec -i --env "TSQL=${tsql}" "${container_id}" bash -euc '
    /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "$MSSQL_SA_PASSWORD" -C -b -h -1 -W -Q "SET NOCOUNT ON; $TSQL"
  ')"
  # Remove CR e espaços das pontas; escalares do sqlcmd vêm em uma única linha.
  printf '%s' "${raw}" | tr -d '\r' | sed -e 's/^[[:space:]]*//' -e 's/[[:space:]]*$//'
}

wait_for_instance() {
  local container_id="$1" label="$2" attempt
  for attempt in {1..30}; do
    if docker exec -i "${container_id}" bash -euc \
      '/opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "$MSSQL_SA_PASSWORD" -C -b -l 5 -Q "SELECT 1" -o /dev/null' 2>/dev/null; then
      return 0
    fi
    sleep 2
  done
  echo "Instância ${label} não respondeu a tempo." >&2
  exit 1
}

echo "== 1/7  Verificando instâncias =="
wait_for_instance "${primary_id}" "${primary_service}"
wait_for_instance "${replica_id}" "${replica_service}"

hadr_primary="$(query_scalar "${primary_id}" "SELECT SERVERPROPERTY('IsHadrEnabled')")"
hadr_replica="$(query_scalar "${replica_id}" "SELECT SERVERPROPERTY('IsHadrEnabled')")"
if [[ "${hadr_primary}" != "1" || "${hadr_replica}" != "1" ]]; then
  echo "Always On não está habilitado (IsHadrEnabled primário=${hadr_primary}, réplica=${hadr_replica})." >&2
  echo "Garanta MSSQL_ENABLE_HADR=1 (overlay) e recrie os containers antes de continuar." >&2
  exit 1
fi

primary_name="$(query_scalar "${primary_id}" "SELECT CONVERT(sysname, SERVERPROPERTY('ServerName'))")"
replica_name="$(query_scalar "${replica_id}" "SELECT CONVERT(sysname, SERVERPROPERTY('ServerName'))")"
if [[ -z "${primary_name}" || -z "${replica_name}" || "${primary_name}" == "${replica_name}" ]]; then
  echo "Não foi possível resolver nomes distintos das instâncias (primário='${primary_name}', réplica='${replica_name}')." >&2
  exit 1
fi
echo "  primário=${primary_name}  réplica=${replica_name}"

echo "== 2/7  Master key e certificados de endpoint =="
for pair in "${primary_id}:primário" "${replica_id}:réplica"; do
  cid="${pair%%:*}"; role="${pair##*:}"
  run_sql "${cid}" "master key (${role})" "
    IF NOT EXISTS (SELECT 1 FROM master.sys.symmetric_keys WHERE name = '##MS_DatabaseMasterKey##')
      CREATE MASTER KEY ENCRYPTION BY PASSWORD = '${SGE_AG_CERTIFICATE_PASSWORD}';"
  run_sql "${cid}" "certificado dbm_certificate (${role})" "
    IF NOT EXISTS (SELECT 1 FROM master.sys.certificates WHERE name = 'dbm_certificate')
      CREATE CERTIFICATE dbm_certificate WITH SUBJECT = 'SGE AG endpoint';"
done

echo "== 3/7  Troca de certificados públicos =="
export_cert() {
  local cid="$1" prefix="$2"
  docker exec -i --env "CERT_PASSWORD=${SGE_AG_CERTIFICATE_PASSWORD}" "${cid}" bash -euc '
    rm -f /var/opt/mssql/data/dbm_certificate.cer /var/opt/mssql/data/dbm_certificate.pvk
    /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "$MSSQL_SA_PASSWORD" -C -b -Q "
      BACKUP CERTIFICATE dbm_certificate
        TO FILE = N''/var/opt/mssql/data/dbm_certificate.cer''
        WITH PRIVATE KEY (
          FILE = N''/var/opt/mssql/data/dbm_certificate.pvk'',
          ENCRYPTION BY PASSWORD = N''$CERT_PASSWORD'');"
  ' >/dev/null
  docker cp "${cid}:/var/opt/mssql/data/dbm_certificate.cer" "${work_dir}/${prefix}.cer" >/dev/null
  docker cp "${cid}:/var/opt/mssql/data/dbm_certificate.pvk" "${work_dir}/${prefix}.pvk" >/dev/null
  docker exec -i "${cid}" rm -f /var/opt/mssql/data/dbm_certificate.cer /var/opt/mssql/data/dbm_certificate.pvk
}
import_cert() {
  local cid="$1" prefix="$2" peer="$3"
  docker cp "${work_dir}/${prefix}.cer" "${cid}:/var/opt/mssql/data/${prefix}.cer" >/dev/null
  docker cp "${work_dir}/${prefix}.pvk" "${cid}:/var/opt/mssql/data/${prefix}.pvk" >/dev/null
  docker exec -i "${cid}" bash -euc 'chown mssql:mssql "/var/opt/mssql/data/${0}.cer" "/var/opt/mssql/data/${0}.pvk"' "${prefix}" || true
  run_sql "${cid}" "importa certificado do ${peer}" "
    IF NOT EXISTS (SELECT 1 FROM master.sys.certificates WHERE name = '${prefix}_dbm_certificate')
      CREATE CERTIFICATE ${prefix}_dbm_certificate
        FROM FILE = '/var/opt/mssql/data/${prefix}.cer'
        WITH PRIVATE KEY (
          FILE = '/var/opt/mssql/data/${prefix}.pvk',
          DECRYPTION BY PASSWORD = '${SGE_AG_CERTIFICATE_PASSWORD}');"
  docker exec -i "${cid}" rm -f "/var/opt/mssql/data/${prefix}.cer" "/var/opt/mssql/data/${prefix}.pvk"
}

export_cert "${primary_id}" "primary"
export_cert "${replica_id}" "replica"
import_cert "${replica_id}" "primary" "primário"
import_cert "${primary_id}" "replica" "réplica"

echo "== 4/7  Endpoints de espelhamento (porta ${endpoint_port}) =="
create_endpoint() {
  local cid="$1" role="$2"
  run_sql "${cid}" "endpoint Hadr_endpoint (${role})" "
    IF NOT EXISTS (SELECT 1 FROM sys.endpoints WHERE name = 'Hadr_endpoint')
      CREATE ENDPOINT [Hadr_endpoint]
        STATE = STARTED
        AS TCP (LISTENER_PORT = ${endpoint_port}, LISTENER_IP = ALL)
        FOR DATABASE_MIRRORING (
          ROLE = ALL,
          AUTHENTICATION = CERTIFICATE dbm_certificate,
          ENCRYPTION = REQUIRED ALGORITHM AES);
    IF EXISTS (SELECT 1 FROM sys.database_mirroring_endpoints WHERE name = 'Hadr_endpoint' AND state_desc <> 'STARTED')
      ALTER ENDPOINT [Hadr_endpoint] STATE = STARTED;"
}
create_endpoint "${primary_id}" "primário"
create_endpoint "${replica_id}" "réplica"

echo "== 5/7  Criação do grupo de disponibilidade =="
ag_exists="$(query_scalar "${primary_id}" "SELECT COUNT(*) FROM sys.availability_groups WHERE name = '${availability_group}'")"
if [[ "${ag_exists}" == "0" ]]; then
  run_sql "${primary_id}" "cria ${availability_group}" "
    CREATE AVAILABILITY GROUP [${availability_group}]
      WITH (CLUSTER_TYPE = NONE)
      FOR REPLICA ON
        N'${primary_name}' WITH (
          ENDPOINT_URL = N'tcp://${primary_service}:${endpoint_port}',
          AVAILABILITY_MODE = SYNCHRONOUS_COMMIT,
          FAILOVER_MODE = MANUAL,
          SEEDING_MODE = AUTOMATIC,
          SECONDARY_ROLE (ALLOW_CONNECTIONS = ALL)),
        N'${replica_name}' WITH (
          ENDPOINT_URL = N'tcp://${replica_service}:${endpoint_port}',
          AVAILABILITY_MODE = SYNCHRONOUS_COMMIT,
          FAILOVER_MODE = MANUAL,
          SEEDING_MODE = AUTOMATIC,
          SECONDARY_ROLE (ALLOW_CONNECTIONS = ALL));"
else
  echo "  ok: ${availability_group} já existe no primário"
fi

joined="$(query_scalar "${replica_id}" "SELECT COUNT(*) FROM sys.availability_groups WHERE name = '${availability_group}'")"
if [[ "${joined}" == "0" ]]; then
  run_sql "${replica_id}" "réplica entra no AG" "ALTER AVAILABILITY GROUP [${availability_group}] JOIN WITH (CLUSTER_TYPE = NONE);"
  run_sql "${replica_id}" "concede CREATE ANY DATABASE" "ALTER AVAILABILITY GROUP [${availability_group}] GRANT CREATE ANY DATABASE;"
else
  echo "  ok: réplica já participa de ${availability_group}"
fi

echo "== 6/7  Inclusão do banco ${database} =="
db_present="$(query_scalar "${primary_id}" "SELECT COUNT(*) FROM sys.databases WHERE name = '${database}'")"
if [[ "${db_present}" == "0" ]]; then
  echo "Banco ${database} não existe no primário; suba a aplicação/migrations antes." >&2
  exit 1
fi
recovery_model="$(query_scalar "${primary_id}" "SELECT recovery_model_desc FROM sys.databases WHERE name = '${database}'")"
if [[ "${recovery_model}" != "FULL" ]]; then
  run_sql "${primary_id}" "recovery model FULL" "ALTER DATABASE [${database}] SET RECOVERY FULL;"
fi
run_sql "${primary_id}" "backup full base para seeding" "
  IF NOT EXISTS (SELECT 1 FROM msdb.dbo.backupset WHERE database_name = '${database}' AND type = 'D')
    BACKUP DATABASE [${database}] TO DISK = N'/var/opt/mssql/backup/${database}_ag_seed.bak' WITH CHECKSUM, INIT, FORMAT;"

in_ag="$(query_scalar "${primary_id}" "SELECT COUNT(*) FROM sys.availability_databases_cluster WHERE database_name = '${database}'")"
if [[ "${in_ag}" == "0" ]]; then
  run_sql "${primary_id}" "adiciona ${database} ao AG" "ALTER AVAILABILITY GROUP [${availability_group}] ADD DATABASE [${database}];"
else
  echo "  ok: ${database} já está no grupo de disponibilidade"
fi

echo "== 7/7  Aguardando sincronização inicial =="
for attempt in {1..60}; do
  state="$(query_scalar "${primary_id}" "
    SELECT TOP 1 drs.synchronization_state_desc
    FROM sys.dm_hadr_database_replica_states drs
    JOIN sys.availability_replicas ar ON ar.replica_id = drs.replica_id
    JOIN sys.databases d ON d.database_id = drs.database_id
    WHERE d.name = '${database}' AND ar.replica_server_name = '${replica_name}'" || true)"
  state="$(echo "${state}" | tr -d '[:space:]')"
  if [[ "${state}" == "SYNCHRONIZED" ]]; then
    echo "  réplica SYNCHRONIZED"
    break
  fi
  if [[ "${attempt}" -eq 60 ]]; then
    echo "Réplica não atingiu SYNCHRONIZED (estado atual: '${state:-desconhecido}')." >&2
    echo "Use scripts/verify-replica-sync.sh para acompanhar o seeding." >&2
    exit 1
  fi
  sleep 5
done

echo
echo "Grupo de disponibilidade ${availability_group} configurado."
echo "Verifique a saúde com: wsl bash scripts/verify-replica-sync.sh"
echo "Failover manual documentado em docs/backup-and-replication.md."
