# Sistema de Gestão Empresarial Hospitalar — Backend

Backend hospitalar em .NET 10 LTS, Clean Architecture, SQL Server, Redis,
RabbitMQ/MassTransit e OpenTelemetry. A base já inclui modelo organizacional
multi-hospital, autenticação com sessão persistida, autorização configurável,
auditoria HTTP e publicação confiável pela Outbox. O desenho e as decisões de segurança estão em
[`docs/architecture.md`](docs/architecture.md).

O checklist de go-live, a política de retenção e o procedimento comprovável de
backup/restore estão em [`docs/production-readiness.md`](docs/production-readiness.md).

## Estado atual

- autenticação JWT com refresh token rotativo e autoridade real da sessão no SQL/Redis;
- somente uma sessão ativa por usuário e timeout deslizante por inatividade;
- autorização por permissões, deny-by-default e proteção contra autoelevação;
- auditoria assíncrona de metadados mínimos, sem captura de corpos ou PII de rede;
- Transactional Outbox com publicação at-least-once pelo Worker;
- Inbox durável, consumer idempotente, classificação de falhas e auditoria de mensagens;
- funcionários com matrícula gerada pelo SQL e vínculos históricos multi-hospital;
- health checks, logs estruturados, métricas e traces OpenTelemetry;
- Nginx, SQL Server, Redis, RabbitMQ, API, Worker e Collector executáveis via Compose;
- testes unitários, integração e validação arquitetural de segurança.

## Desenvolvimento

Pré-requisitos para execução sem containers: SDK .NET 10.0.400 e as dependências
externas configuradas por environment. Para restaurar e testar:

```powershell
dotnet tool restore
dotnet restore
dotnet test
```

Para o ambiente local containerizado, copie `.env.example` para `.env`, substitua
todos os valores `CHANGE_ME` por segredos locais fortes, preencha
`SGE_ASPIRE_OTLP_API_KEY` com 32 bytes aleatórios (por exemplo, gerados com
`openssl rand -hex 32`) e execute:

```powershell
docker compose up -d --build
```

O Nginx é a única entrada pública da API: `http://localhost:8080` redireciona para
`https://localhost:8443`. O certificado autogerado é exclusivamente local e exigirá
confiança explícita do cliente; em qualquer ambiente compartilhado, desabilite
`SGE_NGINX_GENERATE_SELF_SIGNED_CERTIFICATE` e monte um certificado emitido por uma
CA confiável. Swagger fica em `/swagger` apenas no ambiente de desenvolvimento.
SQL Server, Redis, RabbitMQ Management e OTLP não são publicados nem mesmo pelo
override local. Nginx também publica somente em `127.0.0.1`, nas portas 8080/8443.
O único health check público no Nginx é `/nginx-health`;
`/health/ready` permanece acessível apenas na rede interna para o orquestrador.

Quando o Docker estiver disponível somente no WSL:

```powershell
wsl -d Ubuntu -- bash -lc 'cd /mnt/c/caminho/do/repositorio && docker compose up -d --build --wait'
```

Para iniciar também o frontend irmão no Windows, use PowerShell 7:

```powershell
pwsh ./scripts/dev-up.ps1
pwsh ./scripts/dev-up.ps1 -SkipFrontend
pwsh ./scripts/dev-down.ps1
```

Os scripts derivam os diretórios a partir do repositório e usam Docker Engine na
distribuição WSL `Ubuntu` (ajustável por `-Distro`). O frontend escuta somente em
`127.0.0.1:4200`. O script de subida verifica o schema inicial antes de iniciar a
aplicação; migrations posteriores continuam sendo aplicadas pelo job explícito.
`-Bootstrap` executa o provisionamento administrativo já documentado. A parada
preserva volumes; `-Down -Volumes` exige confirmação explícita para apagar dados.
A sessão keepalive mantém a distribuição ativa; `-Full` a encerra. O guardião
opcional `scripts/wsl-keepalive-guardian.ps1` só deve ser iniciado quando necessário.

Não abra bindings `0.0.0.0`, regras de firewall ou portproxy para contornar problemas
de localhost. Verifique o Docker na distribuição e o encaminhamento de localhost
[documentado pelo WSL](https://learn.microsoft.com/en-us/windows/wsl/wsl-config).
`.env`, variantes locais e `CREDENCIAIS-DEV-LOCAL.md` ficam fora do Git e do contexto
de build Docker; o arquivo da senha de bootstrap deve continuar fora do repositório.

Os testes rápidos não exigem infraestrutura. A suíte concorrente real é habilitada
explicitamente por `SGE_REAL_INFRASTRUCTURE_TESTS=true` e recebe conexões pelas
variáveis `SGE_TEST_SQLSERVER`, `SGE_TEST_REDIS` e `SGE_TEST_RABBITMQ_*`. No CI, o
Compose inicia dependências isoladas antes dessa categoria. Localmente, quando o
Docker estiver disponível somente no WSL, execute:

```powershell
wsl bash ./scripts/run-real-integration-tests-wsl.sh
```

Sem a flag explícita, esses testes são marcados como ignorados para evitar que uma
execução aparentemente integrada use dependências inexistentes.

## Perímetro HTTP e IP real

O Nginx termina TLS, limita conexões, requests e payloads, aplica timeouts e headers
de segurança e sobrescreve `X-Forwarded-For` com o endereço observado na conexão.
A API não publica porta no host e aceita forwarded headers somente do IP fixo do
Nginx na rede Docker interna `sge-api-proxy`. `RemoteIpAddress`, rate limiting,
sessões e `ApiRequestLog.IpOrigem` usam, portanto, o IP do cliente já validado pelo
middleware, não o IP do proxy e nem um header fornecido pelo cliente.

Há ainda uma segunda camada de rate limiting no ASP.NET Core: limite global por identidade autenticada ou IP
e política mais restritiva em `/api/auth/login` e `/api/auth/refresh`. Kestrel limita
o body a 1 MiB, o tempo de headers, keep-alive e a taxa mínima de leitura. Os mesmos
limites principais existem no Nginx para rejeitar abuso antes de atingir a aplicação.

O arquivo base do Compose usa `Production`, rede interna, Redis autenticado, usuário
SQL de runtime sem DDL e nenhum serviço publicado. Para produção, use também
`docker-compose.production.yml`, forneça certificado e chave confiáveis pelos paths
`SGE_TLS_CERTIFICATE_PATH`/`SGE_TLS_PRIVATE_KEY_PATH`, configure
`SGE_NGINX_SERVER_NAME` e mantenha `ReverseProxy:KnownProxies` restrito aos proxies
reais. O `docker-compose.override.yml`, carregado automaticamente no desenvolvimento,
é o único local em que o certificado autogerado é habilitado.

## Autenticação

Endpoints disponíveis:

```text
POST /api/auth/login
POST /api/auth/refresh
POST /api/auth/logout
GET  /api/auth/me
```

Access tokens são JWTs curtos, refresh tokens são opacos e rotacionados, e somente
hashes dos tokens são persistidos. A validade real depende da sessão SQL + Redis;
um JWT assinado não é suficiente. O logout exige uma sessão ativa e revoga todas as
sessões inconsistentes ainda marcadas como ativas.

`GET /api/auth/me` exige sessão ativa e retorna somente a identidade pública do
usuário, funcionário/organização/unidade de contratação quando existentes, versão
de permissões e permissões efetivas. Senha, hashes, IDs internos e dados da sessão
não fazem parte da resposta.

Nenhum usuário ou segredo administrativo é criado automaticamente pela migration e
não existem credenciais padrão no código ou no banco. O primeiro administrador é
provisionado por um comando one-shot separado, descrito abaixo.

## Bootstrap administrativo inicial

Depois das migrations e antes de disponibilizar a API, configure no `.env` os
campos `SGE_BOOTSTRAP_*`. Grave uma senha exclusiva de pelo menos 16 caracteres em
um arquivo fora do repositório e informe seu caminho WSL em
`SGE_BOOTSTRAP_PASSWORD_FILE`. A senha deve conter maiúscula, minúscula, número e
caractere especial. Em seguida execute:

```powershell
wsl bash ./scripts/bootstrap-initial-admin-docker.sh
```

O job usa o login SQL de runtime, monta a senha como arquivo read-only e cria, na
mesma transação, organização, unidade hospitalar, profissão, cargo, funcionário,
usuário e o perfil `ADMINISTRADOR_INICIAL` com todas as permissões ativas do
catálogo. A execução grava `AuditLog` e Outbox sem incluir a senha ou seu hash.

O banco adquire lock exclusivo durante a operação. Se qualquer usuário — inclusive
soft-deleted — já existir, o comando termina sem alterar dados e retorna código 3.
Por isso, ele não é um mecanismo de recuperação nem de criação de administradores
adicionais. Remova o arquivo da senha após a execução e use o fluxo administrativo
autenticado para operações posteriores.

## Autorização configurável

Endpoints administrativos disponíveis, todos protegidos pela permissão
`USUARIO_GERENCIAR_PERMISSOES`:

```text
GET /api/usuarios?search=&active=&page=1&pageSize=50
GET /api/usuarios/{userGuid}/permissions
PUT /api/usuarios/{userGuid}/permissions/{permissionCode}
```

O corpo do `PUT` recebe `{ "granted": true|false }`. Negação direta prevalece
sobre perfis, autoalteração é bloqueada e o administrador só pode conceder uma
permissão que também possua. Mudanças incrementam a versão durável do usuário e
instalam uma barreira no Redis antes do commit, impedindo autorização com cache
antigo durante invalidações concorrentes.

A listagem de usuários é paginada, aceita busca por nome, e-mail ou matrícula e
retorna somente usuários vinculados à mesma organização do administrador. Usuários
de outras organizações não são revelados.

## Catálogos organizacionais para o frontend

Os catálogos abaixo expõem somente identificadores públicos (`Guid`) e aplicam o
escopo organizacional derivado do usuário autenticado:

```text
GET /api/organizacoes/atual
GET /api/unidades-hospitalares?search=&active=&page=1&pageSize=50
GET /api/unidades-hospitalares/{unitGuid}
GET /api/setores?search=&active=&unitGuid=&page=1&pageSize=50
GET /api/setores/{sectorGuid}
```

Organização e unidades exigem `FUNCIONARIO_VISUALIZAR`. Setores exigem
`SETOR_VISUALIZAR`. Leituras por `Guid` de outra organização respondem como não
encontradas, evitando revelar a existência de dados cross-tenant. Listagens são
paginadas no servidor, limitadas a 100 registros por página e nunca retornam dados
de outra organização.

Antes de iniciar a API pela primeira vez, aplique migrations de forma controlada:

```powershell
$env:SGE_DESIGNTIME_SQLSERVER = "Server=localhost,1433;Database=SistemaGestaoEmpresarial;User Id=sa;Password=<senha-local>;Encrypt=True;TrustServerCertificate=True"
dotnet tool run dotnet-ef database update --project src/Sistema.Gestao.Empresarial.Infrastructure
```

Com Docker disponível no WSL e o stack de dependências iniciado, o migration job
pode ser executado isoladamente, sem alterar os artefatos `bin/obj` do host:

```powershell
wsl bash ./scripts/apply-migrations-docker.sh
```

Não execute migration automaticamente em cada réplica. Em deploy, use um job único
e controlado. O `docker-compose.override.yml` publica o Nginx e a UI administrativa
do Aspire em `127.0.0.1:18888`; as portas de dados e OTLP continuam privadas.

## Auditoria HTTP

Requisições autorizadas enfileiram somente método, endpoint sem query, status,
duração, identificador do usuário, ambiente, `CorrelationId`, `TraceId` e tipo de
exceção. Corpos, headers, query string, IP e User-Agent não são persistidos. Uma fila
limitada grava esses metadados fora do caminho da requisição e descarta com alerta
quando saturada, evitando que indisponibilidade do SQL amplifique tráfego hostil.

## Publicação da Outbox

O Worker reivindica mensagens pendentes em lotes com lock SQL e lease, publica os
envelopes versionados no RabbitMQ via MassTransit e atualiza o registro para
`PUBLICADA` sem removê-lo. Falhas técnicas usam backoff exponencial; payload ou
metadados inconsistentes ficam em `ERRO_PERMANENTE` para diagnóstico. A entrega é
at-least-once e todas as tentativas preservam o mesmo `MessageId`, preparando a
deduplicação durável pela Inbox.

## Integração contínua

O workflow `.github/workflows/ci.yml` valida formatação, build Release, testes,
dependências vulneráveis, script idempotente de migrations, configuração do Compose
e build das imagens da API e do Worker. Os resultados TRX e o script SQL são
publicados como artefatos temporários junto à cobertura Cobertura. Uma etapa
separada executa concorrência e atomicidade contra SQL Server, Redis e RabbitMQ
reais. As credenciais usadas no Compose são efêmeras, geradas e mascaradas em cada
execução.

O workflow `.github/workflows/codeql.yml` analisa os arquivos do GitHub Actions e o
código C# com build manual baseado no `global.json` e restore travado. Workflows
genéricos de aplicação desktop e de `Dockerfile` na raiz não são utilizados, pois
não representam a arquitetura deste backend.

## Funcionários e vínculos multi-hospital

Os endpoints usam somente `Guid` como identificador público e são protegidos pelas
permissões `FUNCIONARIO_VISUALIZAR`, `FUNCIONARIO_CRIAR` e `FUNCIONARIO_EDITAR`:

```text
GET   /api/funcionarios
GET   /api/funcionarios/{employeeGuid}
POST  /api/funcionarios
PUT   /api/funcionarios/{employeeGuid}
PATCH /api/funcionarios/{employeeGuid}/status
POST  /api/funcionarios/{employeeGuid}/unidades-atuacao
POST  /api/funcionarios/{employeeGuid}/unidades-atuacao/{relationshipGuid}/encerrar
POST  /api/funcionarios/{employeeGuid}/setores
POST  /api/funcionarios/{employeeGuid}/setores/{relationshipGuid}/encerrar
```

A unidade de contratação é a origem imutável do vínculo e não limita a atuação.
Unidades de atuação podem ser quaisquer hospitais ativos da mesma organização. Um
setor exige atuação ativa na unidade correspondente. Encerramentos informam data,
inativam o relacionamento e preservam todo o histórico; não existem endpoints
HTTP `DELETE`. Cada alteração persiste `AuditLog` e `OutboxMessage` na mesma
transação da mudança de domínio.

O escopo do ator é resolvido no servidor pelo vínculo
`Usuário → Funcionário → Unidade de contratação → Organização`. Listagens, leituras,
mutações e administração de permissões negam por padrão atores sem esse vínculo e
não retornam objetos pertencentes a outra organização.

## Catálogos profissionais

Profissões e cargos são configuráveis, auditáveis e nunca removidos fisicamente.
Níveis profissionais permanecem estruturados pelos registros `JR`, `PL` e `SR` e
possuem consulta própria. Todos os endpoints exigem permissões específicas:

```text
GET   /api/profissoes
GET   /api/profissoes/{professionGuid}
POST  /api/profissoes
PUT   /api/profissoes/{professionGuid}
PATCH /api/profissoes/{professionGuid}/status

GET   /api/cargos
GET   /api/cargos/{positionGuid}
POST  /api/cargos
PUT   /api/cargos/{positionGuid}
PATCH /api/cargos/{positionGuid}/status

GET   /api/niveis-profissionais
GET   /api/niveis-profissionais/{levelGuid}
```

Atualizações e mudanças de status são idempotentes. Uma profissão ou cargo usado
por funcionário ativo não pode ser inativado. Toda mudança efetiva grava
`AuditLog` e `OutboxMessage` na mesma transação SQL.

## Inbox, retry e DLQ

O consumer `IntegrationEventConsumer` usa a chave única `(MessageId, Consumer)` e
lock de linha no SQL Server para impedir efeitos duplicados entre réplicas. Cada
tentativa fica preservada em `InboxMessages` e `MessageAuditLogs`.

- regra de negócio: `REJEITADA_REGRA_NEGOCIO`, auditoria e ACK;
- validação conhecida: `REJEITADA_VALIDACAO`, auditoria e ACK;
- falha técnica transitória: retry exponencial configurável;
- falha permanente ou retry esgotado: status `DLQ` e fila durável
  `sge-integration-events-v1_error` do MassTransit.

Nenhuma mensagem, tentativa ou auditoria é fisicamente apagada. Os testes
`RealInfrastructure` exercitam concorrência e atomicidade usando SQL Server, Redis
e RabbitMQ reais.

## Aspire Dashboard no Docker Compose

`docker compose up -d --build` inicia o Dashboard standalone automaticamente pelo
`docker-compose.override.yml`. Acesse `http://localhost:18888` e obtenha o browser
token gerado a cada inicialização com:

```bash
docker compose logs aspire-dashboard
```

Não compartilhe os logs de inicialização nem versione o token. A UI exige
`BrowserToken`; a chave `SGE_ASPIRE_OTLP_API_KEY` é exclusivamente para ingestão,
não é o token de login. Guarde-a no `.env` ignorado pelo Git. O Compose recusa
valor ausente/vazio. `docker compose config --quiet` valida sem imprimir segredos;
a saída completa de `docker compose config` contém os valores interpolados.

```text
API / Worker → OTLP/gRPC → otel-collector:4317 → aspire-dashboard:18889
```

O Collector carrega `deploy/otel-collector-config.yml` e mescla somente o exporter
e as listas de exporters de `deploy/otel-collector-aspire.yml`. Traces, métricas e
logs usam o header `x-otlp-api-key`, interpolado do ambiente pelo Collector. O
transporte HTTP/gRPC sem TLS fica restrito à rede Docker interna `default`; a API
e o Worker mantêm endpoint, instrumentação e sampling existentes. O `debug` em
`basic` continua apenas no fluxo base/produção/CI para diagnóstico por contagens;
no fluxo local o Aspire o substitui, evitando saída contínua duplicada do Collector.

A imagem oficial `13.5.2` (compatível com o AppHost `13.5.3`, cuja tag de container
não estava publicada) está fixada também por digest. Executa como usuário não root,
com filesystem somente leitura, `/tmp` em memória e limites de recursos. A UI
publica somente `127.0.0.1:18888`; nenhuma porta OTLP é publicada. A rede exclusiva
`dashboard-access` permite o NAT dessa porta, indisponível em bridges marcadas
`internal: true`. Só o Dashboard participa dela; não há conexão à `edge`,
`api-proxy`, Nginx ou socket Docker. A imagem distroless não contém cliente HTTP
para executar healthcheck: usa-se ordem de startup e fila/retry do exporter.

Para produção, selecione explicitamente os arquivos abaixo (sem o override local):

```bash
docker compose -f docker-compose.yml -f docker-compose.production.yml up -d --build
```

Esse fluxo e o CI com `docker-compose.ci.yml` não incluem Dashboard, rede de acesso,
chave Aspire ou exporter para um serviço inexistente. A validação do override local
no CI recebe uma chave efêmera mascarada. Não combine o override local com produção.

O Dashboard mantém telemetria em memória: reiniciar perde os dados e exige novo
login. A coleta existente não habilita corpos, headers ou parâmetros SQL sensíveis,
mas logs de exceções e scopes não têm sanitização geral; trate a telemetria como
informação administrativa sensível. O AppHost e seu fluxo de desenvolvimento
continuam independentes e não são executados dentro do Compose.

Referências: [segurança do Dashboard](https://aspire.dev/dashboard/security-considerations/),
[imagem oficial](https://github.com/dotnet/dotnet-docker/blob/main/README.aspire-dashboard.md)
e [mesclagem e variáveis do Collector](https://opentelemetry.io/docs/collector/configuration/).

## Desenvolvimento com .NET Aspire

O AppHost oferece uma segunda experiência de desenvolvimento local para iniciar e
observar API, Worker, SQL Server, Redis e RabbitMQ em um único Dashboard. Ele exige
o SDK .NET `10.0.400` definido em `global.json`, certificado de desenvolvimento
HTTPS confiável, Docker Engine (inclusive nativo no WSL) ou Podman acessível no
ambiente onde o AppHost é executado, e a Aspire CLI `13.5.3`. O AppHost usa Aspire `13.5.3`, versão estável
compatível com `net10.0`. Instale ou atualize a CLI com:

```powershell
dotnet tool install --global Aspire.Cli --version 13.5.3
# Se já estiver instalada:
dotnet tool update --global Aspire.Cli --version 13.5.3
```

Configure valores locais exclusivos, nunca credenciais de produção. Na primeira
execução, o Dashboard solicita os parâmetros ausentes; marque a opção para gravar
os valores no User Secrets do AppHost. Os nomes de configuração são:

```text
SGE_SQLSERVER_SA_PASSWORD
SGE_SQLSERVER_APP_USERNAME
SGE_SQLSERVER_APP_PASSWORD
SGE_REDIS_USERNAME
SGE_REDIS_PASSWORD
SGE_RABBITMQ_USERNAME
SGE_RABBITMQ_PASSWORD
SGE_JWT_SIGNING_KEY
```

Também é possível fornecê-los como variáveis de ambiente do processo. Senhas não
devem ser passadas como argumentos de linha de comando nem gravadas em
`appsettings*.json`. A senha SQL administrativa é usada somente pelo container e
pelo job de migrations; API e Worker recebem exclusivamente o login restrito da
aplicação. O Redis inicia com o usuário `default` desabilitado e uma ACL limitada
ao namespace `sge*`. RabbitMQ mantém o vhost `/sge`.

Inicie a partir da raiz do repositório:

```powershell
dotnet run --project src/Sistema.Gestao.Empresarial.AppHost
```

O comando abre o Dashboard em `https://localhost:17088`, protegido pelo browser
token gerado pelo Aspire e limitado ao loopback. O fluxo de startup aguarda SQL,
cria o login restrito, aplica migrations, valida Redis/RabbitMQ com autenticação e
então inicia API e Worker. A API aparece diretamente no Dashboard para uso local;
esse endpoint não representa o perímetro de produção, que continua no Nginx.
Logs, traces e métricas usam a instrumentação OpenTelemetry já existente e apenas
trocam o destino OTLP para o Dashboard durante essa execução.

Para parar, pressione `Ctrl+C`. SQL Server, Redis e RabbitMQ usam volumes locais
nomeados para sobreviver a reinicializações. Depois de parar o AppHost, limpe-os
explicitamente quando quiser recriar todo o ambiente:

```powershell
docker volume rm sge-aspire-sql-data sge-aspire-redis-data sge-aspire-rabbitmq-data
```

As portas dinâmicas dos serviços de dados são vinculadas somente ao host local
para que os projetos executados pelo AppHost possam acessá-las. O endpoint de
Management do RabbitMQ não é publicado. Nenhuma porta do Dashboard ou recurso
Aspire foi adicionada ao Nginx.

> .NET Aspire é utilizado como ferramenta de desenvolvimento e orquestração local. A infraestrutura e as políticas de segurança de produção continuam definidas pelos artefatos de deployment do projeto.

O Docker Compose existente permanece a referência para a topologia containerizada
e para produção, incluindo Nginx, redes internas, limites e hardening. O AppHost
não é usado no deployment e não substitui nenhum arquivo Compose.
