# Sistema de Gestão Empresarial Hospitalar — Backend

Backend hospitalar em .NET 10 LTS, Clean Architecture, SQL Server, Redis,
RabbitMQ/MassTransit e OpenTelemetry. A base já inclui modelo organizacional
multi-hospital, autenticação com sessão persistida, autorização configurável,
auditoria HTTP e publicação confiável pela Outbox. O desenho e as decisões de segurança estão em
[`docs/architecture.md`](docs/architecture.md).

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
todos os valores `CHANGE_ME` por segredos locais fortes e execute:

```powershell
docker compose up --build
```

O Nginx é a única entrada pública da API: `http://localhost:8080` redireciona para
`https://localhost:8443`. O certificado autogerado é exclusivamente local e exigirá
confiança explícita do cliente; em qualquer ambiente compartilhado, desabilite
`SGE_NGINX_GENERATE_SELF_SIGNED_CERTIFICATE` e monte um certificado emitido por uma
CA confiável. Swagger fica em `/swagger` apenas no ambiente de desenvolvimento.
SQL Server, Redis, RabbitMQ Management e OTLP não são publicados nem mesmo pelo
override local. O único health check público no Nginx é `/nginx-health`;
`/health/ready` permanece acessível apenas na rede interna para o orquestrador.

Quando o Docker estiver disponível somente no WSL:

```powershell
wsl -d Ubuntu -- bash -lc 'cd /mnt/c/caminho/do/repositorio && docker compose up -d --build --wait'
```

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
```

Access tokens são JWTs curtos, refresh tokens são opacos e rotacionados, e somente
hashes dos tokens são persistidos. A validade real depende da sessão SQL + Redis;
um JWT assinado não é suficiente. O logout exige uma sessão ativa e revoga todas as
sessões inconsistentes ainda marcadas como ativas.

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

Endpoints administrativos disponíveis, ambos protegidos pela permissão
`USUARIO_GERENCIAR_PERMISSOES`:

```text
GET /api/usuarios/{userGuid}/permissions
PUT /api/usuarios/{userGuid}/permissions/{permissionCode}
```

O corpo do `PUT` recebe `{ "granted": true|false }`. Negação direta prevalece
sobre perfis, autoalteração é bloqueada e o administrador só pode conceder uma
permissão que também possua. Mudanças incrementam a versão durável do usuário e
instalam uma barreira no Redis antes do commit, impedindo autorização com cache
antigo durante invalidações concorrentes.

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
e controlado. O `docker-compose.override.yml` publica somente o Nginx para
conveniência local; as portas administrativas e de dados continuam privadas.

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
