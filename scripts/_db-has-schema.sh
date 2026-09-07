#!/usr/bin/env bash
# Sai com 0 se o banco SistemaGestaoEmpresarial ja tem a tabela __EFMigrationsHistory
# com pelo menos uma migracao aplicada; caso contrario sai com 1.
# Usado por dev-up.ps1 para decidir se roda apply-migrations-docker.sh.
set -uo pipefail

project_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
set -a
# shellcheck disable=SC1090
source "${project_root}/.env"
set +a

sql="SET NOCOUNT ON;
IF DB_ID('SistemaGestaoEmpresarial') IS NULL
  BEGIN PRINT 'NO_DB'; RETURN; END
IF NOT EXISTS (SELECT 1 FROM SistemaGestaoEmpresarial.sys.tables WHERE name = '__EFMigrationsHistory')
  BEGIN PRINT 'NO_TABLE'; RETURN; END
DECLARE @n int;
SELECT @n = COUNT(*) FROM SistemaGestaoEmpresarial.dbo.__EFMigrationsHistory;
IF @n > 0 PRINT 'HAS_SCHEMA' ELSE PRINT 'EMPTY';"

out="$(docker compose --project-directory "${project_root}" exec -T sqlserver \
  /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "${SGE_SQLSERVER_SA_PASSWORD}" \
  -C -b -h -1 -W -Q "${sql}" 2>&1)"

echo "${out}"
case "${out}" in
  *HAS_SCHEMA*) exit 0 ;;
  *) exit 1 ;;
esac
