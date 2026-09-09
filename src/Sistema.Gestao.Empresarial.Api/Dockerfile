FROM mcr.microsoft.com/dotnet/sdk:10.0@sha256:4ea6fe75dd36706bb6d8c3c293d4c4315840f5d76ea28ac97def77e3ec487fa5 AS build
WORKDIR /src
COPY ["Directory.Build.props", "."]
COPY ["src/Sistema.Gestao.Empresarial.Domain/Sistema.Gestao.Empresarial.Domain.csproj", "src/Sistema.Gestao.Empresarial.Domain/"]
COPY ["src/Sistema.Gestao.Empresarial.Domain/packages.lock.json", "src/Sistema.Gestao.Empresarial.Domain/"]
COPY ["src/Sistema.Gestao.Empresarial.Application/Sistema.Gestao.Empresarial.Application.csproj", "src/Sistema.Gestao.Empresarial.Application/"]
COPY ["src/Sistema.Gestao.Empresarial.Application/packages.lock.json", "src/Sistema.Gestao.Empresarial.Application/"]
COPY ["src/Sistema.Gestao.Empresarial.Infrastructure/Sistema.Gestao.Empresarial.Infrastructure.csproj", "src/Sistema.Gestao.Empresarial.Infrastructure/"]
COPY ["src/Sistema.Gestao.Empresarial.Infrastructure/packages.lock.json", "src/Sistema.Gestao.Empresarial.Infrastructure/"]
COPY ["src/Sistema.Gestao.Empresarial.Api/Sistema.Gestao.Empresarial.Api.csproj", "src/Sistema.Gestao.Empresarial.Api/"]
COPY ["src/Sistema.Gestao.Empresarial.Api/packages.lock.json", "src/Sistema.Gestao.Empresarial.Api/"]
RUN dotnet restore "src/Sistema.Gestao.Empresarial.Api/Sistema.Gestao.Empresarial.Api.csproj" --locked-mode
COPY . .
RUN dotnet publish "src/Sistema.Gestao.Empresarial.Api/Sistema.Gestao.Empresarial.Api.csproj" -c Release -o /app/publish --no-restore /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0@sha256:a4556ed033fa96f984bb7a8d348851cb2d36b1281dd2420070045f664fbb5f94 AS final
RUN apt-get update \
    && apt-get install --no-install-recommends -y curl \
    && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=build /app/publish .
USER app
EXPOSE 8080
ENTRYPOINT ["dotnet", "Sistema.Gestao.Empresarial.Api.dll"]
