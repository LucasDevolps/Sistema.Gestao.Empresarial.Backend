# Publicação local no IIS (coexistindo com o ambiente de desenvolvimento)

Este documento descreve como publicar a aplicação (API .NET 10 + SPA Angular) no
**IIS do Windows**, para uso normal do sistema **sem manter nenhuma IDE, `dotnet run`
ou `ng serve` aberta**, e **sem alterar** o ambiente de desenvolvimento atual
(Docker/WSL2, Aspire, OpenTelemetry, portas, banco).

O ambiente publicado é **adicional**, não substitui nada. Tudo é dirigido por um
único script idempotente: [`scripts/publish-local-iis.ps1`](../scripts/publish-local-iis.ps1).

---

## 1. Arquitetura

### Desenvolvimento (permanece exatamente como hoje)

```text
IDE / dotnet run / ng serve
        |
        v
Docker Engine dentro do WSL2 (Ubuntu)  -- docker compose (base + override)
        |
        +-- SQL Server        (rede interna; sem porta no host)
        +-- Redis             (rede interna; sem porta no host)
        +-- RabbitMQ          (rede interna; sem porta no host)
        +-- OpenTelemetry     (rede interna; sem porta no host)
        +-- API + Worker      (containers)
        +-- Nginx    -> 127.0.0.1:8080 / 127.0.0.1:8443
        +-- Aspire Dashboard -> 127.0.0.1:18888
ng serve (Node do Windows) -> http://localhost:4200
```

### Aplicação publicada no IIS (novo, adicional)

```text
Browser
  |
  v
IIS  http://localhost:9080   Site "SistemaGestaoEmpresarial-Frontend"
  |     |
  |     +-- SPA Angular (arquivos estáticos publicados)
  |     +-- /api  e  /health   --(URL Rewrite + ARR, mesma origem)-->
  |
  v
IIS  http://127.0.0.1:9081   Site "SistemaGestaoEmpresarial-Api"  (.NET 10, ANCM in-process)
  |
  +--> 127.0.0.1:11433  --NAT loopback do WSL-->  MESMO container SQL Server do Docker
  +--> 127.0.0.1:16379  --NAT loopback do WSL-->  MESMO container Redis do Docker
  +--> 127.0.0.1:15673  --NAT loopback do WSL-->  MESMO broker RabbitMQ do Docker
  +--> 127.0.0.1:14317  --NAT loopback do WSL-->  MESMO OpenTelemetry Collector do Docker
```

Pontos-chave:

- **Mesmo banco.** A API do IIS usa o container SQL Server que já roda no Docker e o
  database `SistemaGestaoEmpresarial`. Nenhum SQL Server novo, nenhum volume novo,
  nenhuma recriação, nenhum seed destrutivo.
- **Mesma infra.** Redis, RabbitMQ e o Collector são os mesmos do desenvolvimento.
- **Mesma origem.** O browser fala só com `localhost:9080`. O site do frontend faz
  reverse proxy de `/api` e `/health` para a API. Nenhum CORS é introduzido (a API
  não registra política de CORS — mesma decisão do Nginx do projeto).
- **Só loopback.** Todo bind novo é `127.0.0.1`. Nada é publicado para a LAN.
- **Production.** A API publica com `DOTNET_ENVIRONMENT=Production`: Swagger
  desativado, sem Developer Exception Page, sem stack trace.

---

## 2. Portas

| Camada | Item | Desenvolvimento (inalterado) | Publicado no IIS |
| --- | --- | --- | --- |
| Frontend | Angular | `ng serve` `http://localhost:4200` | **`http://localhost:9080`** (site IIS) |
| Backend | API | Nginx `127.0.0.1:8080` / `127.0.0.1:8443`; Kestrel dev `5145/7193` | **`http://127.0.0.1:9081`** (site IIS) |
| Observabilidade | Aspire Dashboard | `127.0.0.1:18888` | inalterado (reaproveitado) |
| Infra p/ IIS | SQL Server | rede interna do Docker (sem host) | **`127.0.0.1:11433`** → container `1433` |
| Infra p/ IIS | Redis | rede interna do Docker (sem host) | **`127.0.0.1:16379`** → container `6379` |
| Infra p/ IIS | RabbitMQ (AMQP) | rede interna do Docker (sem host) | **`127.0.0.1:15673`** → container `5672` |
| Infra p/ IIS | OTLP gRPC | rede interna do Docker (sem host) | **`127.0.0.1:14317`** → container `4317` |
| Infra p/ IIS | OTLP HTTP | rede interna do Docker (sem host) | **`127.0.0.1:14318`** → container `4318` |

O RabbitMQ Management (`15672`), o SA do SQL Server e a UI/portas OTLP **não** são
publicados para lugar nenhum.

Todas as portas são **parâmetros** do script (ver seção 6). O script **verifica**
cada porta antes de usar (processos do Windows, sites do IIS e containers do Docker)
e **aborta com a origem do conflito** — nunca escolhe outra porta em silêncio.

As portas `9080`/`9081` e as `1xxxx` de infra foram escolhidas fora de tudo que o
repositório usa hoje: `4200`, `5145`, `7193`, `8080`, `8443`, `18888`, `1433`,
`6379`, `5672`, `4317`, `4318`, `15672`.

---

## 3. Como a infra do Docker (dentro do WSL2) fica acessível ao IIS (no Windows)

O overlay [`docker-compose.iis.yml`](../docker-compose.iis.yml) **complementa** o
Compose atual. Ele:

- cria uma bridge adicional **não interna** `iis-access` e anexa a ela apenas
  `sqlserver`, `redis`, `rabbitmq` e `otel-collector` — **mesmo padrão** já usado
  pelo `aspire-dashboard` + `dashboard-access` no `docker-compose.override.yml`;
- publica as portas desses serviços **somente em `127.0.0.1`** (loopback do WSL);
- **não** altera a rede `default` (continua `internal: true`), nem credenciais,
  virtual host, ACLs ou volumes.

Como o WSL2 encaminha `127.0.0.1` entre Windows e a distro (`localhostForwarding`,
ligado por padrão), a API no IIS alcança `127.0.0.1:11433` e chega ao SQL Server do
container. O script valida essa conectividade (`Test-NetConnection`) antes de subir
os sites.

O overlay é aplicado assim (o script faz isto por você):

```powershell
docker compose `
  -f docker-compose.yml -f docker-compose.override.yml -f docker-compose.iis.yml `
  up -d --no-deps sqlserver redis rabbitmq otel-collector
```

> Ao aplicar o overlay pela primeira vez, esses 4 containers são **recriados** para
> ganhar as portas/rede novas (interrupção de poucos segundos). Execuções seguintes
> são idempotentes. **Nenhum volume é tocado; o banco é preservado.**

Para **remover** as portas de loopback (voltar a expor a infra só nas redes internas):

```powershell
docker compose -f docker-compose.yml -f docker-compose.override.yml `
  up -d --no-deps sqlserver redis rabbitmq otel-collector
```

---

## 4. Segredos

O script **reutiliza a estratégia já existente**: lê o arquivo **`.env`** da raiz do
repositório (o mesmo consumido pelo `docker compose`; fora do Git por `.gitignore`).

Chaves obrigatórias (o script valida apenas **presença**, nunca imprime valores):

```
SGE_SQLSERVER_APP_USERNAME   SGE_SQLSERVER_APP_PASSWORD
SGE_REDIS_USERNAME           SGE_REDIS_PASSWORD
SGE_RABBITMQ_USERNAME        SGE_RABBITMQ_PASSWORD
SGE_JWT_SIGNING_KEY
```

Os valores são injetados como **variáveis de ambiente do Application Pool** da API,
gravadas em `applicationHost.config` (arquivo de sistema, ACL só de Administradores/
SYSTEM). **Nada** de segredo vai para `appsettings*.json`, `web.config`, o diretório
publicado, o repositório ou os logs. O log do script mascara segredos e senhas
embutidas em connection strings.

---

## 5. Pré-requisitos e o que o script instala

**Você precisa ter:** Windows 10/11 ou Windows Server, WSL2 com a distro `Ubuntu` e
o ambiente de desenvolvimento Docker **no ar** (`scripts/dev-up.ps1`), **.NET SDK
`10.0.400`** no Windows (é o pino exato de `global.json`, `rollForward: latestPatch` —
não basta um SDK 10.0.x qualquer; instale de `dot.net/v1/dotnet-install.ps1
-Version 10.0.400 -InstallDir "C:\Program Files\dotnet"`), Node ≥ 20.19 (validado
com Node 22) e o `.env` preenchido.

> **Windows PowerShell 5.1** é o interpretador de referência (`powershell.exe`) — os
> cmdlets DISM/`WebAdministration` são nativos nele. O script já força
> `WSL_UTF8=1` (senão `wsl -l -q` volta ilegível no 5.1).

**O script instala/configura (idempotente, pulável em `-DryRun`):**

| Componente | Verificação | Origem |
| --- | --- | --- |
| Recursos do IIS (Web Server, Static Content, Default Document, HTTP Errors, Request Filtering, ISAPI, WebSockets, Management Console + Scripting) | `Get-WindowsOptionalFeature` | Windows Optional Features (mínimo necessário) |
| ASP.NET Core Module v2 + runtime ASP.NET Core 10 | runtime `shared\Microsoft.AspNetCore.App\10.*` **+** ANCM v2 registrado (`%ProgramFiles%\IIS\Asp.Net Core Module\V2\aspnetcorev2.dll` **ou** `inetsrv\aspnetcorev2.dll` — bundles atuais **não** copiam mais para `inetsrv`) | **.NET 10 Hosting Bundle**, download de `aka.ms`/`microsoft.com`, assinatura Authenticode Microsoft verificada; instalação silenciosa; `net stop was` / `net start w3svc`. Se o IIS acabou de ser instalado (ou os recursos ISAPI foram ligados agora), o Hosting Bundle é (re)parado para registrar o ANCM (recomendação oficial). |
| URL Rewrite 2.1 | `Get-WebGlobalModule RewriteModule` | `download.microsoft.com` (oficial), assinatura verificada |
| Application Request Routing 3.0 | `Get-WebGlobalModule ApplicationRequestRouting` | `download.microsoft.com` (oficial), assinatura verificada |

O proxy do ARR é habilitado a nível de servidor (`system.webServer/proxy` com
`preserveHostHeader=true`), **sem nenhuma regra catch-all de saída** — só as regras
`^(api|health)` do site do frontend encaminham para a API. Instaladores podem ser
fornecidos offline com `-HostingBundleInstaller` / `-UrlRewriteInstaller` / `-ArrInstaller`.

**Long paths.** O script assume `HKLM\SYSTEM\CurrentControlSet\Control\FileSystem
\LongPathsEnabled = 1` (o `dotnet publish` e o `npm ci` geram árvores > 260 chars).
Ligue uma vez: `Set-ItemProperty 'HKLM:\SYSTEM\CurrentControlSet\Control\FileSystem'
LongPathsEnabled 1`. O stage do `dotnet publish` foi movido para `C:\_sgepub\<hora>`
(caminho curto) e limpo ao final.

**Firewall / LAN.** O firewall **não** é alterado e nenhuma regra de entrada é
criada. Os sites têm binding só nos loopbacks `127.0.0.1` **e** `[::1]` (IPv6 — veja
seção 13). Requisições a qualquer outro endereço (LAN, IP público) chegam ao
http.sys mas **não casam com nenhum site** → `HTTP 400`, nunca conteúdo. Para fechar
a porta também no nível TCP seria preciso `netsh http add iplisten` (afeta *todos*
os sites da máquina) ou uma regra de bloqueio de firewall — nenhum dos dois é feito.

---

## 6. Instalar / publicar

Abra o **PowerShell como Administrador** (o script aborta se não estiver elevado) e,
na raiz do repositório do backend:

```powershell
# padrão (9080 / 9081 e portas de infra 11433/16379/15673/14317/14318)
.\scripts\publish-local-iis.ps1

# escolhendo portas
.\scripts\publish-local-iis.ps1 -FrontendPort 9080 -BackendPort 9081

# frontend em outro diretório
.\scripts\publish-local-iis.ps1 -FrontendPath "C:\source\Sistema.Gestao.Empresarial.Frontend"

# ver TUDO que seria feito, sem alterar nada
.\scripts\publish-local-iis.ps1 -DryRun
```

> Prefira o **Windows PowerShell 5.1** (`powershell.exe`) para a primeira execução —
> ele traz DISM e `WebAdministration` nativos. O PowerShell 7 (`pwsh`) também
> funciona, via camada de compatibilidade do Windows.

### Parâmetros

| Parâmetro | Padrão | Uso |
| --- | --- | --- |
| `-Action` | `Publish` | `Publish` \| `Start` \| `Stop` \| `Status` \| `Remove` |
| `-FrontendPort` | `9080` | porta HTTP (loopback) do site do frontend |
| `-BackendPort` | `9081` | porta HTTP (loopback) do site da API |
| `-FrontendPath` | `C:\Users\lucas\source\repos\Sistema.Gestao.Empresarial.Frontend` | repositório do frontend (fonte da verdade) |
| `-BackendPath` | pasta acima de `\scripts` | raiz do repositório do backend |
| `-IisRoot` | `C:\inetpub\SistemaGestaoEmpresarial` | raiz física (`\Api`, `\Frontend`) |
| `-WslDistribution` | `Ubuntu` | distro WSL com o Docker |
| `-SqlServerLoopbackPort` | `11433` | porta host do SQL Server |
| `-RedisLoopbackPort` | `16379` | porta host do Redis |
| `-RabbitMqLoopbackPort` | `15673` | porta host do RabbitMQ AMQP |
| `-OtlpGrpcLoopbackPort` | `14317` | porta host do OTLP gRPC |
| `-OtlpHttpLoopbackPort` | `14318` | porta host do OTLP HTTP |
| `-HostingBundleInstaller` / `-UrlRewriteInstaller` / `-ArrInstaller` | — | instaladores locais (uso offline) |
| `-SkipTests` | desligado | pula `dotnet test` / `ng test` (não recomendado) |
| `-DryRun` | desligado | só analisa e valida; não altera nada |

### O que a publicação faz, em ordem

1. valida sessão elevada e conflitos de porta;
2. valida WSL + distro;
3. valida Docker e infraestrutura de dev no ar;
4. aplica `docker-compose.iis.yml` (portas loopback) e valida conectividade Windows→WSL;
5. valida o banco (`scripts/_db-has-schema.sh`) — aborta se não houver schema; **nunca migra**;
6. instala/valida IIS, Hosting Bundle, URL Rewrite, ARR;
7. `dotnet restore` / `build -c Release` / `test` (falha de teste aborta a publicação);
8. `npm ci` / build de produção do Angular (`npm run build`);
9. cria/atualiza os Application Pools (`No Managed Code`, 64-bit, `ApplicationPoolIdentity`);
10. injeta as variáveis de ambiente da API no pool (segredos fora do repositório);
11. **deploy atômico**: `dotnet publish` em pasta temporária → valida → `app_offline.htm`
    → `robocopy /MIR` → remove `app_offline` (backup `.bak` + rollback se a cópia falhar);
12. publica o Angular e gera o `web.config` do frontend (SPA fallback + proxy `/api`,`/health`
    via ARR; **sem** `<serverVariables>` — a seção `allowedServerVariables` é travada
    em escopo de servidor e num web.config de site derruba o módulo com "500 URL
    Rewrite Module Error");
13. cria/atualiza os sites IIS com binding duplo de loopback — `127.0.0.1:<porta>` **e**
    `[::1]:<porta>` — aplica hardening e ACL mínima;
14. sobe os pools/sites e roda os health checks (contra `http://localhost:<porta>`);
    imprime a tabela final.

Idempotente: rodar de novo **atualiza** os mesmos sites/pools. Nunca cria
`...-Api-2`. Não toca em outros sites nem no `Default Web Site`.

---

## 7. Fluxo de atualização do dia a dia

```powershell
git pull
.\scripts\publish-local-iis.ps1
```

---

## 8. Operação

```powershell
.\scripts\publish-local-iis.ps1 -Action Status   # sites, pools, portas de infra, /health/live
.\scripts\publish-local-iis.ps1 -Action Stop     # para só os sites/pools do SGE (Docker e dev intactos)
.\scripts\publish-local-iis.ps1 -Action Start    # religa os sites/pools do SGE
.\scripts\publish-local-iis.ps1 -Action Remove   # remove sites, pools e env vars do SGE no IIS
```

`Remove` **não** apaga o diretório `C:\inetpub\SistemaGestaoEmpresarial` (apague à
mão se quiser), **não** desinstala IIS/Hosting Bundle/URL Rewrite/ARR (podem servir
a outros sites) e **não** mexe no Docker, no banco nem nos volumes.

### Logs

`logs\publish-iis-AAAAMMDD-HHMMSS.log` na raiz do backend (pasta já coberta por
`.gitignore`). Segredos e senhas em connection strings são mascarados. Também há
`logs\robocopy-api.log` e `logs\robocopy-frontend.log`.

### Health checks usados

| Alvo | Esperado |
| --- | --- |
| `http://localhost:9081/health/live` | 200 |
| `http://localhost:9081/health/ready` | 200 (SQL + Redis alcançáveis) |
| `http://localhost:9080/` | 200, contém `app-root` |
| `http://localhost:9080/login` | 200 (fallback SPA) |
| `http://localhost:9080/health/ready` | 200 (proxy same-origin) |
| `http://localhost:9081/swagger/index.html` | 404/401 (Swagger **não** exposto em Production) |
| Application Pools | `Started` |

Falha em qualquer item obrigatório → `exit code != 0`.

> **Sempre `localhost`, não `127.0.0.1`.** Num site com binding de **IP específico**,
> o http.sys valida o `Host` header contra o binding e devolve `HTTP 400 – Invalid
> Hostname` para `Host: 127.0.0.1`. Como `localhost` resolve para `::1` **antes** de
> `127.0.0.1` no Windows, os sites são publicados com **dois** bindings de loopback
> (`127.0.0.1:<porta>` e `[::1]:<porta>`); sem o binding IPv6, navegador/curl batem
> em `::1`, o http.sys não acha site e responde 400 (o cliente não faz fallback).
> Ambos os endereços são não-roteáveis → continua sem exposição na LAN.

---

## 9. Voltar ao desenvolvimento

Nada a desfazer. O IIS é **adicional**:

- `docker compose up` / `scripts/dev-up.ps1` continuam funcionando nas portas de sempre
  (`4200`, `8080`, `8443`, `18888`);
- a IDE, o Aspire e o OpenTelemetry continuam iguais;
- se quiser, `.\scripts\publish-local-iis.ps1 -Action Stop` desliga os sites do IIS;
  o ambiente de dev não depende deles.

O overlay `docker-compose.iis.yml` só publica portas **extras** em loopback; ele não
altera nenhuma porta existente. Para retirá-las, veja o fim da seção 3.

---

## 10. Componentes preservados (não foram alterados)

| Área | Decisão |
| --- | --- |
| **Migrations** | O projeto aplica migrations **fora do processo** (`scripts/apply-migrations-docker.sh`); a aplicação **não** migra no startup. O script do IIS **só valida** que o schema existe e **aborta** se não existir. IDE e IIS nunca disputam o schema. |
| **Worker** | Continua **no Docker** (container `worker`). A API publicada **não** vira host do Worker e **não** consome RabbitMQ. Assim não há consumidor duplicado do vhost `/sge`. Ver seção 11. |
| **OpenTelemetry** | Mesma instrumentação, mesmo Collector. Só muda o endpoint da API do IIS: `OpenTelemetry__OtlpEndpoint = http://127.0.0.1:14317`. Nenhum trace/metric/log é desativado. |
| **Aspire** | O `AppHost` é orquestração de desenvolvimento; a API publicada não depende dele. Nada removido, nada duplicado. |
| **Redis / RabbitMQ** | Instâncias existentes. Sem ACL, usuário, senha, `instanceName` ou virtual host novos. Portas só em loopback, só se necessário. |
| **Autenticação/Autorização** | Inalteradas. Tokens continuam em memória no SPA (Bearer no header). Sem CORS, sem `AllowAnyOrigin`, sem `*`. |
| **Reverse proxy** | A API mantém `ReverseProxy:Enabled=true` + `KnownProxies=127.0.0.1` para ler `X-Forwarded-*` vindos do site do frontend (ARR). Rate limiting, IP forwarding e headers de segurança seguem valendo. |
| **Regras de negócio** | Nenhuma alteração em entidades, handlers, controllers, DTOs, permissões ou schema. |

---

## 11. Coexistência: o que pode rodar junto

| Cenário | Situação |
| --- | --- |
| **A** — Docker + infra + aplicação pela IDE (`dev-up.ps1`) | Funciona como hoje. O IIS não precisa estar no ar. |
| **B** — Docker + infra + aplicação pelo IIS | Funciona. IDE fechada. Frontend em `localhost:9080`. |
| **C** — Docker + IDE + IIS ao mesmo tempo | **Sem conflito de portas** (conjuntos disjuntos) e **mesmo SQL/Redis** sem conflito de rede. |

### Cuidados no Cenário C (dois backends ao mesmo tempo)

Rodar a API da IDE **e** a API do IIS simultaneamente é possível a nível de rede,
mas atente para:

- **Migrations** — não execute `apply-migrations-docker.sh` enquanto qualquer das
  duas APIs estiver no ar sob carga. As APIs não migram sozinhas, então o risco é
  só o da execução manual.
- **Worker / consumers RabbitMQ** — mantenha **um só** Worker (o do Docker). Não
  suba um segundo Worker no Windows; dois consumidores no vhost `/sge` mudam o
  comportamento de entrega (Inbox/Outbox/idempotência).
- **Rotinas agendadas** (`AuditRetentionWorker`, `OutboxPublisherWorker`) vivem no
  Worker do Docker — não são duplicadas pela API do IIS.
- **Rate limiting** é por processo; cada API tem o seu contador. Isso é esperado.
- **Sessões/JWT** — as duas APIs compartilham a mesma `Jwt__SigningKey` e o mesmo
  Redis/SQL de sessão, então um token emitido por uma vale na outra. É intencional.

Resumo: **duas APIs full podem coexistir**; **um único Worker**; **migrations sempre
manuais e isoladas**.

---

## 12. Arquivos deste recurso

| Arquivo | Tipo | Conteúdo |
| --- | --- | --- |
| `scripts/publish-local-iis.ps1` | novo | script único idempotente (`Publish`/`Start`/`Stop`/`Status`/`Remove`, `-DryRun`) |
| `docker-compose.iis.yml` | novo | overlay que publica SQL/Redis/RabbitMQ/OTLP **só em `127.0.0.1`** via bridge `iis-access` |
| `docs/local-iis-deployment.md` | novo | este documento |

Nenhum arquivo de aplicação, `appsettings*.json`, `Program.cs`, migration,
`docker-compose.yml`/`override`/`production` ou configuração de desenvolvimento foi
alterado.
