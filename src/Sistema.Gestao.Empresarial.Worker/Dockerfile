FROM mcr.microsoft.com/dotnet/sdk:10.0@sha256:4beef5b8919dcaa2dc924233bd069257e883cc7a061e09088a97d152d6a48510 AS build
WORKDIR /src
COPY ["Directory.Build.props", "."]
COPY ["src/Sistema.Gestao.Empresarial.Domain/Sistema.Gestao.Empresarial.Domain.csproj", "src/Sistema.Gestao.Empresarial.Domain/"]
COPY ["src/Sistema.Gestao.Empresarial.Domain/packages.lock.json", "src/Sistema.Gestao.Empresarial.Domain/"]
COPY ["src/Sistema.Gestao.Empresarial.Application/Sistema.Gestao.Empresarial.Application.csproj", "src/Sistema.Gestao.Empresarial.Application/"]
COPY ["src/Sistema.Gestao.Empresarial.Application/packages.lock.json", "src/Sistema.Gestao.Empresarial.Application/"]
COPY ["src/Sistema.Gestao.Empresarial.Infrastructure/Sistema.Gestao.Empresarial.Infrastructure.csproj", "src/Sistema.Gestao.Empresarial.Infrastructure/"]
COPY ["src/Sistema.Gestao.Empresarial.Infrastructure/packages.lock.json", "src/Sistema.Gestao.Empresarial.Infrastructure/"]
COPY ["src/Sistema.Gestao.Empresarial.Worker/Sistema.Gestao.Empresarial.Worker.csproj", "src/Sistema.Gestao.Empresarial.Worker/"]
COPY ["src/Sistema.Gestao.Empresarial.Worker/packages.lock.json", "src/Sistema.Gestao.Empresarial.Worker/"]
RUN dotnet restore "src/Sistema.Gestao.Empresarial.Worker/Sistema.Gestao.Empresarial.Worker.csproj" --locked-mode
COPY . .
RUN dotnet publish "src/Sistema.Gestao.Empresarial.Worker/Sistema.Gestao.Empresarial.Worker.csproj" -c Release -o /app/publish --no-restore /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0@sha256:011bb5f30180717b1c8b65822ff2c99bcb96bc65af0164589751b83c7b4949f7 AS final
RUN apt-get update \
    && apt-get install --no-install-recommends -y curl \
    && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=build /app/publish .
USER app
EXPOSE 8081
ENTRYPOINT ["dotnet", "Sistema.Gestao.Empresarial.Worker.dll"]
