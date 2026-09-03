#!/usr/bin/env bash
set -euo pipefail

app_user="${SGE_SQLSERVER_APP_USERNAME:?SGE_SQLSERVER_APP_USERNAME is required}"
app_password="${SGE_SQLSERVER_APP_PASSWORD:?SGE_SQLSERVER_APP_PASSWORD is required}"
sa_password="${SGE_SQLSERVER_SA_PASSWORD:?SGE_SQLSERVER_SA_PASSWORD is required}"

if [[ ! "$app_user" =~ ^[A-Za-z][A-Za-z0-9_]{0,63}$ ]]; then
  echo "SGE_SQLSERVER_APP_USERNAME must be a simple SQL identifier." >&2
  exit 64
fi

escaped_password=${app_password//\'/\'\'}

for attempt in {1..30}; do
  if /opt/mssql-tools18/bin/sqlcmd \
      -S sqlserver -U sa -P "$sa_password" -C -b -l 3 \
      -Q "SET NOCOUNT ON; SELECT 1" -o /dev/null 2>/dev/null; then
    break
  fi
  if [[ "$attempt" -eq 30 ]]; then
    echo "SQL Server não ficou disponível dentro do prazo." >&2
    exit 1
  fi
  sleep 2
done

/opt/mssql-tools18/bin/sqlcmd \
  -S sqlserver -U sa -P "$sa_password" -C -b \
  -Q "
IF DB_ID(N'SistemaGestaoEmpresarial') IS NULL
    CREATE DATABASE [SistemaGestaoEmpresarial];
IF NOT EXISTS (SELECT 1 FROM sys.server_principals WHERE name = N'$app_user')
    CREATE LOGIN [$app_user] WITH PASSWORD = N'$escaped_password', CHECK_POLICY = ON, CHECK_EXPIRATION = OFF;
"

for attempt in {1..30}; do
  if /opt/mssql-tools18/bin/sqlcmd \
      -S sqlserver -U sa -P "$sa_password" -C -b -l 3 \
      -d SistemaGestaoEmpresarial \
      -Q "SET NOCOUNT ON; SELECT 1" -o /dev/null 2>/dev/null; then
    break
  fi
  if [[ "$attempt" -eq 30 ]]; then
    echo "O banco da aplicação não ficou disponível dentro do prazo." >&2
    exit 1
  fi
  sleep 2
done

/opt/mssql-tools18/bin/sqlcmd \
  -S sqlserver -U sa -P "$sa_password" -C -b \
  -d SistemaGestaoEmpresarial \
  -Q "
IF SCHEMA_ID(N'sge') IS NULL
    EXEC(N'CREATE SCHEMA [sge] AUTHORIZATION [dbo]');
IF NOT EXISTS (SELECT 1 FROM sys.database_principals WHERE name = N'$app_user')
    CREATE USER [$app_user] FOR LOGIN [$app_user];
IF IS_ROLEMEMBER(N'db_datareader', N'$app_user') = 1
    ALTER ROLE [db_datareader] DROP MEMBER [$app_user];
IF IS_ROLEMEMBER(N'db_datawriter', N'$app_user') = 1
    ALTER ROLE [db_datawriter] DROP MEMBER [$app_user];
GRANT SELECT, INSERT, UPDATE ON SCHEMA::[sge] TO [$app_user];
REVOKE ALTER, CONTROL ON SCHEMA::[sge] TO [$app_user];
DENY DELETE ON SCHEMA::[sge] TO [$app_user];
"
