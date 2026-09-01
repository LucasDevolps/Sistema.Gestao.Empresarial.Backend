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

docker run --rm \
  --network "${SGE_DOCKER_NETWORK:-sge-network}" \
  --volume "${project_root}:/source:ro" \
  --workdir /workspace \
  --env "SGE_DESIGNTIME_SQLSERVER=Server=sqlserver,1433;Database=SistemaGestaoEmpresarial;User Id=sa;Password=${SGE_SQLSERVER_SA_PASSWORD};Encrypt=True;TrustServerCertificate=True" \
  mcr.microsoft.com/dotnet/sdk:10.0 \
  bash -lc "tar --exclude='bin' --exclude='obj' -C /source -cf - . | tar -C /workspace -xf - && dotnet tool restore && dotnet restore src/Sistema.Gestao.Empresarial.Infrastructure/Sistema.Gestao.Empresarial.Infrastructure.csproj --locked-mode && dotnet tool run dotnet-ef database update --project src/Sistema.Gestao.Empresarial.Infrastructure --startup-project src/Sistema.Gestao.Empresarial.Infrastructure --context AppDbContext"
