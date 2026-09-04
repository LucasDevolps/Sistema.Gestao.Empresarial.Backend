#!/usr/bin/env bash
set -euo pipefail

project_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
environment_file="${project_root}/.env"

if [[ ! -f "${environment_file}" ]]; then
  echo "Arquivo .env não encontrado. Crie-o a partir de .env.example." >&2
  exit 1
fi

sqlserver_container="$(docker compose --project-directory "${project_root}" ps -q sqlserver)"
docker_network="$(docker inspect "${sqlserver_container}" --format '{{range $name, $_ := .NetworkSettings.Networks}}{{$name}}{{println}}{{end}}' | head -n 1)"
if [[ -z "${docker_network}" ]]; then
  echo "Não foi possível resolver a rede privada do SQL Server." >&2
  exit 1
fi

egress_network="sge-migrations-egress-$$"
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
  bash -lc 'export SGE_DESIGNTIME_SQLSERVER="Server=sqlserver,1433;Database=SistemaGestaoEmpresarial;User Id=sa;Password=\"${SGE_SQLSERVER_SA_PASSWORD}\";Encrypt=True;TrustServerCertificate=True"
    tar --exclude="bin" --exclude="obj" -C /source -cf - . | tar -C /workspace -xf -
    dotnet tool restore
    dotnet restore src/Sistema.Gestao.Empresarial.Infrastructure/Sistema.Gestao.Empresarial.Infrastructure.csproj --locked-mode
    dotnet tool run dotnet-ef database update --project src/Sistema.Gestao.Empresarial.Infrastructure --startup-project src/Sistema.Gestao.Empresarial.Infrastructure --context AppDbContext'
