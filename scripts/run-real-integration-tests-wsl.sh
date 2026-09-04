#!/usr/bin/env bash
set -euo pipefail

project_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
environment_file="${project_root}/.env"

if [[ ! -f "${environment_file}" ]]; then
  echo "Arquivo .env não encontrado. Crie-o a partir de .env.example." >&2
  exit 1
fi

docker compose \
  --project-directory "${project_root}" \
  -f "${project_root}/docker-compose.yml" \
  up -d --wait sqlserver redis rabbitmq

sqlserver_container="$(docker compose --project-directory "${project_root}" ps -q sqlserver)"
docker_network="$(docker inspect "${sqlserver_container}" --format '{{range $name, $_ := .NetworkSettings.Networks}}{{$name}}{{println}}{{end}}' | head -n 1)"
if [[ -z "${docker_network}" ]]; then
  echo "Não foi possível resolver a rede privada do SQL Server." >&2
  exit 1
fi

egress_network="sge-real-tests-egress-$$"
docker network create "${egress_network}" >/dev/null
cleanup() {
  docker network rm "${egress_network}" >/dev/null 2>&1 || true
}
trap cleanup EXIT

docker run --rm \
  --network "${docker_network}" \
  --network "${egress_network}" \
  --env-file "${environment_file}" \
  --volume "${project_root}:/source:ro" \
  --workdir /workspace \
  mcr.microsoft.com/dotnet/sdk:10.0@sha256:e1ffd2a92ae84c1291bc1b6887501f8af98e6331e7af6d4c8d37168c5e87a64c \
  bash -lc 'export SGE_REAL_INFRASTRUCTURE_TESTS=true
    export SGE_TEST_SQLSERVER="Server=sqlserver,1433;Database=master;User Id=sa;Password=\"${SGE_SQLSERVER_SA_PASSWORD}\";Encrypt=True;TrustServerCertificate=True"
    export SGE_TEST_REDIS="redis:6379,user=${SGE_REDIS_USERNAME},password=${SGE_REDIS_PASSWORD},abortConnect=false"
    export SGE_TEST_RABBITMQ_HOST=rabbitmq SGE_TEST_RABBITMQ_PORT=5672 SGE_TEST_RABBITMQ_VIRTUAL_HOST=/sge
    export SGE_TEST_RABBITMQ_USERNAME="${SGE_RABBITMQ_USERNAME}"
    export SGE_TEST_RABBITMQ_PASSWORD="${SGE_RABBITMQ_PASSWORD}"
    tar --exclude="bin" --exclude="obj" -C /source -cf - . | tar -C /workspace -xf -
    dotnet restore Sistema.Gestao.Empresarial.sln --locked-mode
    dotnet test tests/Sistema.Gestao.Empresarial.IntegrationTests --configuration Release --no-restore --filter "Category=RealInfrastructure"'
