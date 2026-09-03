#!/usr/bin/env bash
set -euo pipefail

project_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
environment_file="${project_root}/.env"

if [[ ! -f "${environment_file}" ]]; then
  echo "Arquivo .env não encontrado. Crie-o a partir de .env.example." >&2
  exit 1
fi

set -a
# shellcheck disable=SC1090
source "${environment_file}"
set +a

required_variables=(
  SGE_SQLSERVER_APP_USERNAME
  SGE_SQLSERVER_APP_PASSWORD
  SGE_BOOTSTRAP_PASSWORD_FILE
  SGE_BOOTSTRAP_ORGANIZATION_NAME
  SGE_BOOTSTRAP_HOSPITAL_UNIT_NAME
  SGE_BOOTSTRAP_PROFESSION_NAME
  SGE_BOOTSTRAP_POSITION_NAME
  SGE_BOOTSTRAP_PROFESSIONAL_LEVEL_CODE
  SGE_BOOTSTRAP_ADMINISTRATOR_NAME
  SGE_BOOTSTRAP_ADMINISTRATOR_EMAIL
  SGE_BOOTSTRAP_ADMISSION_DATE
)

for variable_name in "${required_variables[@]}"; do
  if [[ -z "${!variable_name:-}" ]]; then
    echo "A variável ${variable_name} é obrigatória." >&2
    exit 1
  fi
done

password_file="$(realpath "${SGE_BOOTSTRAP_PASSWORD_FILE}")"
if [[ ! -f "${password_file}" ]]; then
  echo "O arquivo SGE_BOOTSTRAP_PASSWORD_FILE não existe ou não é um arquivo regular." >&2
  exit 1
fi

docker run --rm \
  --network "${SGE_DOCKER_NETWORK:-sge-network}" \
  --volume "${project_root}:/source:ro" \
  --volume "${password_file}:/run/secrets/initial-admin-password:ro" \
  --workdir /workspace \
  --env "SGE_BOOTSTRAP_SQLSERVER=Server=sqlserver,1433;Database=SistemaGestaoEmpresarial;User Id=${SGE_SQLSERVER_APP_USERNAME};Password=${SGE_SQLSERVER_APP_PASSWORD};Encrypt=True;TrustServerCertificate=True" \
  --env "SGE_BOOTSTRAP_PASSWORD_FILE=/run/secrets/initial-admin-password" \
  --env SGE_BOOTSTRAP_ORGANIZATION_NAME \
  --env SGE_BOOTSTRAP_HOSPITAL_UNIT_NAME \
  --env SGE_BOOTSTRAP_PROFESSION_NAME \
  --env SGE_BOOTSTRAP_POSITION_NAME \
  --env SGE_BOOTSTRAP_PROFESSIONAL_LEVEL_CODE \
  --env SGE_BOOTSTRAP_ADMINISTRATOR_NAME \
  --env SGE_BOOTSTRAP_ADMINISTRATOR_EMAIL \
  --env SGE_BOOTSTRAP_ADMINISTRATOR_PHONE \
  --env SGE_BOOTSTRAP_ADMISSION_DATE \
  --security-opt no-new-privileges:true \
  --cap-drop ALL \
  --pids-limit 256 \
  --memory 768m \
  --cpus 1.5 \
  mcr.microsoft.com/dotnet/sdk:10.0@sha256:e1ffd2a92ae84c1291bc1b6887501f8af98e6331e7af6d4c8d37168c5e87a64c \
  bash -lc "tar --exclude='bin' --exclude='obj' -C /source -cf - . | tar -C /workspace -xf - && dotnet restore src/Sistema.Gestao.Empresarial.Bootstrap/Sistema.Gestao.Empresarial.Bootstrap.csproj --locked-mode && dotnet run --project src/Sistema.Gestao.Empresarial.Bootstrap/Sistema.Gestao.Empresarial.Bootstrap.csproj --no-restore --configuration Release"
