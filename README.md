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
- auditoria de request/response com mascaramento de dados sensíveis;
- Transactional Outbox com publicação at-least-once pelo Worker;
- Inbox durável, consumer idempotente, classificação de falhas e auditoria de mensagens;
- health checks, logs estruturados, métricas e traces OpenTelemetry;
- SQL Server, Redis, RabbitMQ, API, Worker e Collector executáveis via Compose;
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

A API fica em `http://localhost:8080`, Swagger em `/swagger` apenas no ambiente de
desenvolvimento, RabbitMQ Management em `http://localhost:15672` por padrão, e os endpoints de
saúde em `/health/live` e `/health/ready`.

Quando o Docker estiver disponível somente no WSL:

```powershell
wsl -d Ubuntu -- bash -lc 'cd /mnt/c/caminho/do/repositorio && docker compose up -d --build --wait'
```

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

Nenhum usuário ou segredo administrativo é criado automaticamente pela migration.
O provisionamento inicial será implementado como fluxo administrativo controlado;
não existem credenciais padrão no código ou no banco.

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
e controlado. O `docker-compose.override.yml` publica portas para conveniência local;
para ensaiar múltiplas réplicas sem conflito de porta, suba somente o arquivo base e
coloque um reverse proxy diante das APIs.

## Auditoria HTTP

Todas as requisições atravessam um middleware que persiste método, endpoint, headers,
query string, request/response body, status, duração, usuário, IP, ambiente,
`CorrelationId` e `TraceId` em `ApiRequestLogs`. A captura é limitada por tamanho e
somente bodies JSON ou form-urlencoded são armazenados; conteúdos binários recebem
um marcador seguro. Senhas, tokens, cookies, API keys, secrets e authorization são
mascarados recursivamente antes da persistência.

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
publicados como artefatos temporários. As credenciais usadas no Compose são efêmeras,
geradas e mascaradas em cada execução.

O workflow `.github/workflows/codeql.yml` analisa os arquivos do GitHub Actions e o
código C# com build manual baseado no `global.json` e restore travado. Workflows
genéricos de aplicação desktop e de `Dockerfile` na raiz não são utilizados, pois
não representam a arquitetura deste backend.

## Inbox, retry e DLQ

O consumer `IntegrationEventConsumer` usa a chave única `(MessageId, Consumer)` e
lock de linha no SQL Server para impedir efeitos duplicados entre réplicas. Cada
tentativa fica preservada em `InboxMessages` e `MessageAuditLogs`.

- regra de negócio: `REJEITADA_REGRA_NEGOCIO`, auditoria e ACK;
- validação conhecida: `REJEITADA_VALIDACAO`, auditoria e ACK;
- falha técnica transitória: retry exponencial configurável;
- falha permanente ou retry esgotado: status `DLQ` e fila durável
  `sge-integration-events-v1_error` do MassTransit.

Nenhuma mensagem, tentativa ou auditoria é fisicamente apagada. A próxima fatia
prevista implementará os casos de uso de funcionários e seus vínculos multi-hospital.
