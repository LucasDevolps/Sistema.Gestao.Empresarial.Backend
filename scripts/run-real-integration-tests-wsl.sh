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

docker compose \
  --project-directory "${project_root}" \
  -f "${project_root}/docker-compose.yml" \
  up -d --wait sqlserver redis rabbitmq

docker run --rm \
  --network "${SGE_DOCKER_NETWORK:-sge-network}" \
  --volume "${project_root}:/source:ro" \
  --workdir /workspace \
  --env "SGE_REAL_INFRASTRUCTURE_TESTS=true" \
  --env "SGE_TEST_SQLSERVER=Server=sqlserver,1433;Database=master;User Id=sa;Password=${SGE_SQLSERVER_SA_PASSWORD};Encrypt=True;TrustServerCertificate=True" \
  --env "SGE_TEST_REDIS=redis:6379,abortConnect=false" \
  --env "SGE_TEST_RABBITMQ_HOST=rabbitmq" \
  --env "SGE_TEST_RABBITMQ_PORT=5672" \
  --env "SGE_TEST_RABBITMQ_USERNAME=${SGE_RABBITMQ_USERNAME}" \
  --env "SGE_TEST_RABBITMQ_PASSWORD=${SGE_RABBITMQ_PASSWORD}" \
  mcr.microsoft.com/dotnet/sdk:10.0 \
  bash -lc "tar --exclude='bin' --exclude='obj' -C /source -cf - . | tar -C /workspace -xf - && dotnet restore Sistema.Gestao.Empresarial.sln --locked-mode && dotnet test tests/Sistema.Gestao.Empresarial.IntegrationTests --configuration Release --no-restore --filter 'Category=RealInfrastructure'"
