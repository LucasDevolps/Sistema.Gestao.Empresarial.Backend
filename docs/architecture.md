# Arquitetura da fundação

## 1. Diagnóstico do repositório

Em 31 de agosto de 2026 o diretório de trabalho está vazio: não existe solution,
projeto, código, configuração, teste, histórico Git ou instrução local `AGENTS.md`.
Não há legado a migrar nem decisões anteriores a preservar. Consequentemente,
todos os requisitos funcionais e não funcionais ainda estão ausentes.

A fundação adotará .NET 10 (`net10.0`), versão LTS ativa, ASP.NET Core, EF Core e
SQL Server. Dependências externas serão encapsuladas em Infrastructure e
configuradas por environment.

## 2. Limites e dependências da solution

```text
Api --------------------> Application ----> Domain
 |                              ^              ^
 +------------------------> Infrastructure ----+

Worker -----------------> Application ----> Domain
 |                              ^
 +------------------------> Infrastructure

UnitTests ----------------------------> Domain/Application
IntegrationTests ---------------------> Api/Worker/Infrastructure
```

Projetos:

- `Domain`: entidades, value objects, eventos de domínio, exceções e regras puras;
- `Application`: casos de uso, DTOs, validação, portas e políticas;
- `Infrastructure`: EF Core/SQL Server, Redis, MassTransit/RabbitMQ, segurança,
  auditoria, outbox, inbox e implementações das portas;
- `Api`: composição HTTP, autenticação/autorização, middleware e OpenAPI;
- `Worker`: publisher da outbox, consumers, checkpoints e rotinas agendadas;
- `UnitTests`: regras determinísticas sem infraestrutura;
- `IntegrationTests`: pipeline HTTP, SQL, Redis e mensageria com dependências reais
  descartáveis quando a respectiva suíte for habilitada.

Controllers serão adaptadores finos. Nenhuma camada interna referencia Api ou
Worker. Domain não referencia EF Core, Redis, RabbitMQ ou ASP.NET Core.

## 3. Arquitetura de execução e containers

```text
                    reverse proxy / ingress
                             |
                 +-----------+-----------+
                 |                       |
              API #1                 API #N
                 +-----------+-----------+
                             |
                  +----------+----------+
                  |          |          |
              SQL Server   Redis     RabbitMQ
                                         |
                             +-----------+-----------+
                             |                       |
                          Worker #1               Worker #N

              API + Worker --OTLP--> OTel Collector
```

API e Worker serão imagens multi-stage, executadas como usuário não root, sem
`container_name`, sem estado necessário em memória local e com graceful shutdown.
O Compose local terá SQL Server, Redis persistente (AOF), RabbitMQ com management,
Collector OTLP, named volumes, health checks e dependências condicionadas à saúde.
Liveness mede o processo; readiness mede SQL, Redis e RabbitMQ conforme a função do
serviço. Migrations serão uma etapa controlada, nunca executadas concorrentemente
por todas as réplicas em produção.

## 4. Modelo organizacional e multi-hospital

```text
Organizacao 1 --- N UnidadeHospitalar 1 --- N Setor
                           |
                           +--- N FuncionarioUnidadeAtuacao N --- 1 Funcionario
                           |                                      |
                           +--- unidade de contratação -----------+

Funcionario 1 --- N FuncionarioSetor N --- 1 Setor
```

`UnidadeContratacaoId` registra origem contratual, não delimita autorização nem
participação. `FuncionarioUnidadeAtuacao` contém início, fim e status e preserva o
histórico. A mesma separação entre origem, escopo de atuação e autorização será
aplicada a pacientes, atendimentos, prescrições, escalas e módulos futuros.
Consultas sempre recebem um escopo organizacional autorizado; pertencer à mesma
organização torna o relacionamento possível, mas não concede acesso por si só.

Organização, unidade, setor e vínculos são entidades configuráveis e não enums.
Uma constraint garante que setor e unidade pertençam à organização coerente; casos
de uso validam que vínculos de atuação não atravessem organizações indevidamente.

## 5. Profissão, cargo, nível e funcionário

- `Profissao`: identidade própria, nome, descrição e ciclo de vida;
- `Cargo`: identidade própria, profissão compatível opcional e ciclo de vida;
- `NivelProfissional`: catálogo estruturado (`JR`, `PL`, `SR`), extensível e sem
  inferência pelo nome do cargo;
- `Funcionario`: matrícula, dados pessoais/profissionais, profissão, cargo, nível e
  unidade de contratação;
- `FuncionarioUnidadeAtuacao` e `FuncionarioSetor`: vínculos temporais e lógicos.

`Id` (`long`) é chave interna; `Guid` é a identidade pública; `Matricula` é gerada
no servidor, imutável e única. A matrícula usará uma `SEQUENCE` do SQL Server (ou
gerador transacional equivalente), nunca contagem de linhas. GUIDs terão índices
únicos. FKs internas continuarão usando `Id`.

## 6. Ciclo de vida sem DELETE

Entidades persistentes implementam metadados de exclusão lógica (`Ativo`,
`Excluido`, `ExcluidoEm`, `ExcluidoPor`). O DbContext aplica filtros globais a todo
tipo soft-deletable. Vínculos são encerrados/inativados, nunca removidos.

O contexto rejeita estados EF `Deleted`, transformando-os em erro, e o contrato de
repositório não oferece `Delete`. Testes arquiteturais procurarão `DELETE`,
`ExecuteDelete`, `Remove` e `RemoveRange` em código de produção. Constraints e
índices filtrados consideram registros não excluídos. `IgnoreQueryFilters()` fica
restrito a serviços explícitos de auditoria/administração.

## 7. Autenticação, JWT e refresh token

`Usuario` e `Funcionario` são agregados distintos com relação opcional 0..1. Senha
é armazenada somente como hash produzido por `PasswordHasher` configurável. Login
tem proteção contra enumeração, limitação de tentativas e auditoria sem segredo.

O access token JWT é curto, assinado com chave rotacionável e contém, no mínimo,
`sub` (UserGuid), `sid` (SessionId), `jti` e `session_version`. O refresh token é
opaco, aleatório, rotacionado a cada uso e somente seu hash é persistido. Reuso de
refresh token revoga a sessão. Refresh nunca revive sessão ausente, revogada,
substituída ou inativa há 30 minutos.

JWT é credencial; a sessão operacional é autoridade. O SQL é a representação
durável/auditável; Redis é a autoridade operacional rápida enquanto saudável.

## 8. Sessão única e concorrência de login

SQL possui índice único filtrado sobre `UsuarioId` para sessão ativa, não revogada e
não excluída. Logins do mesmo usuário são serializados em transação por lock lógico
no SQL Server (`sp_getapplock`, encapsulado em Infrastructure), depois:

1. credenciais são validadas;
2. versões/sessões anteriores são revogadas idempotentemente;
3. a versão de sessão do usuário é incrementada;
4. a nova sessão, auditoria e eventos de outbox são persistidos na mesma transação;
5. o ponteiro ativo no Redis é trocado atomicamente e a sessão anterior é
   invalidada;
6. somente após sucesso durável são emitidos access e refresh tokens ao cliente.

O protocolo Redis usa compare-and-set/Lua para `active-session`, sessão e TTL em um
round trip. Em falhas entre SQL e Redis, a estratégia é fail-closed: não se aceita a
sessão antiga; um reconciliador e o cache miss reidratam a partir do SQL. Constraints
continuam sendo a última barreira contra duas sessões ativas.

Logout revoga todas as sessões ainda ativas do usuário, mesmo havendo a constraint,
e só produz auditoria/evento na primeira transição. Repetições retornam sucesso sem
novas escritas ou eventos.

## 9. Redis, sliding expiration e fallback

Chaves são produzidas exclusivamente por `IRedisKeyFactory`:

```text
sge:{environment}:session:{sessionId}
sge:{environment}:user:{userGuid}:active-session
sge:{environment}:permissions:{userGuid}
sge:{environment}:session-activity:dirty
```

O script de validação compara usuário, `sid`, `jti`, versão, status e ponteiro ativo;
se válido, atualiza `LastActivityAt` e renova TTL para 30 minutos atomicamente. Não
armazena tokens ou segredos em claro. Um checkpoint do Worker consolida atividade
no SQL em intervalo configurável, somente quando a diferença é relevante.

Em cache miss, o SQL é consultado e o Redis reidratado apenas se a sessão durável
ainda for válida. Em indisponibilidade real do Redis, uma política configurável faz
fallback temporário ao SQL com timeout/circuit breaker, métricas e log; falhas
ambíguas são negadas. O fallback privilegia segurança e pode elevar carga, por isso
é observável e limitado.

## 10. Autorização configurável e fail-closed

O modelo contém `Perfil`, `Permissao`, `PerfilPermissao`, `UsuarioPerfil` e
`UsuarioPermissao` (concessão ou negação direta, com precedência explícita). A
autorização usa policies dinâmicas e `RequirePermissionAttribute`, nunca comparação
de role em controllers. O cache Redis mantém um snapshot versionado por usuário,
com TTL curto e atualização compare-and-set. Mudanças incrementam a versão SQL e
instalam no Redis uma barreira `ready=false` antes do commit. Assim, uma requisição
concorrente nunca aceita o snapshot antigo; uma barreira à frente do SQL falha
fechado, enquanto cache ausente ou indisponível pode consultar o SQL.

Uma fallback policy exige usuário autenticado em todo endpoint. Endpoints de negócio
também devem ter permissão explícita; somente ações conscientemente públicas recebem
`AllowAnonymous`. Um teste enumera `EndpointDataSource` e falha quando cada endpoint
não possui exatamente uma justificativa: permissão/policy ou exposição pública. A
concessão de permissões verifica a capacidade administrativa do ator e impede
autoelevação ou concessão além do escopo delegável.

## 11. Auditoria HTTP e de entidades

O middleware cria/preserva `CorrelationId`, usa `Activity.Current.TraceId` para o
trace e captura request/response com limites de tamanho e content-types permitidos.
Um redator recursivo mascara headers, query e JSON sensíveis (`password`, tokens,
cookies, authorization, API keys e secrets) antes da persistência.

`ApiRequestLog` registra metadados, corpos redigidos, status e duração.
`AuditLog` registra entidade/Guid, ação, antes/depois, ator, origem, correlation e
trace. Mudança de domínio + AuditLog + Outbox compartilham a transação SQL. Logs não
são apagados e políticas futuras de arquivamento preservam a proibição de DELETE na
aplicação.

## 12. Contratos, RabbitMQ e outbox

Eventos são DTOs versionados, nunca entidades EF. O envelope contém `eventId`,
`messageId`, tipo/versão, correlation, trace/W3C context, ocorrido em UTC, produtor
e dados. Exchanges e filas são duráveis; mensagens relevantes são persistentes;
quorum queues serão habilitáveis para filas críticas.

`OutboxMessage` guarda envelope/payload, produtor, ator, estado, tentativas, próxima
tentativa e timestamps. O Worker disputa lotes via locking otimista/claim atômico,
publica pelo MassTransit e marca como publicada; nunca apaga. Réplicas podem operar
concomitantemente sem dupla alteração de estado. Confirmações e idempotência do
destino tornam publicação ao menos uma vez segura.

## 13. Inbox, classificação de falhas e DLQ

Cada consumer abre transação, tenta inserir chave única `(MessageId, Consumer)` e:

- duplicada já processada: registra contador, confirma (ACK) e não repete efeito;
- válida: executa efeito, atualiza Inbox e MessageAuditLog e confirma;
- regra de negócio ou validação conhecida: marca
  `REJEITADA_REGRA_NEGOCIO`/`REJEITADA_VALIDACAO`, audita, `LogWarning` e ACK;
- falha técnica transitória: rollback e retry com backoff/jitter;
- falha técnica permanente/desconhecida após política: registra erro e encaminha à
  DLQ, preservando payload e histórico.

`InboxMessage`, `OutboxMessage` e `MessageAuditLog` nunca são apagadas. Um
classificador explícito substitui um `catch (Exception) { throw; }` genérico como
única política. Retry e DLQ são responsabilidade técnica, não mecanismo de regra de
negócio.

## 14. Observabilidade

API, Worker, ASP.NET Core, HttpClient, EF/SqlClient, Redis, MassTransit e operações
próprias (auth, outbox, consumers, checkpoint) emitem traces, métricas e logs
estruturados. Contexto W3C é propagado pelas mensagens. `CorrelationId` funcional e
`TraceId` técnico permanecem distintos e armazenados juntos.

OTLP envia dados ao Collector; nenhuma camada de negócio referencia Datadog. O
Collector poderá exportar futuramente para Datadog, Prometheus/Tempo, Elastic ou
Azure Monitor somente por configuração. Resource attributes incluem service name,
version, environment e instance id; sampling é configurável.

Métricas incluem HTTP/status/duração, auth/sessões, Redis hit/miss/latência/fallback,
mensageria/retry/DLQ, outbox pendente/idade e inbox duplicada/falhas. IDs de usuário,
sessão, mensagem e correlação aparecem apenas em logs/traces seguros, nunca como
labels de alta cardinalidade.

## 15. Persistência inicial

Grupos de tabelas:

- organização: `Organizacoes`, `UnidadesHospitalares`, `Setores`;
- pessoas: `Profissoes`, `Cargos`, `NiveisProfissionais`, `Funcionarios`,
  `FuncionariosUnidadesAtuacao`, `FuncionariosSetores`;
- segurança: `Usuarios`, `UsuariosSessoes`, `Perfis`, `Permissoes`, tabelas de
  associação e histórico de credenciais/refresh quando necessário;
- integração: `OutboxMessages`, `InboxMessages`, `MessageAuditLogs`;
- auditoria: `AuditLogs`, `ApiRequestLogs`.

Todos os horários são `DateTimeOffset` UTC. Índices cobrem Guid, matrícula, chaves
naturais por organização, sessões ativas, outbox pendente e inbox idempotente.
Concorrência otimista usa `rowversion`; invariantes críticas também têm constraints.

## 16. Health, configuração e segurança operacional

`/health/live` é público e só indica vida do processo. `/health/ready` valida as
dependências necessárias; `/health` será protegido ou restrito no ambiente. Swagger
tem Bearer, contratos e responses, é habilitado por opção e não fica público em
produção por padrão. ProblemDetails centraliza falhas sem stack trace em produção.

Options tipadas (`Jwt`, `Session`, `Redis`, `Cache`, `RabbitMq`, `Outbox`, `Inbox`,
`Audit`, `OpenTelemetry`) são validadas no startup. Segredos vêm de environment ou
secret store. SQL usa pooling, timeout e retry transitório configurável.

## 17. Escalabilidade e futuro Kubernetes

Nenhuma regra depende de afinidade de sessão, singleton em memória ou ordem global
de mensagens. SQL/constraints, Redis atômico e Inbox resolvem concorrência entre
APIs e Workers. API e Worker escalam independentemente. Imagens, configuração,
readiness/liveness, shutdown e OTLP já respeitam os contratos esperados por
Deployments, Services, Secrets, ConfigMaps, Jobs de migration e autoscaling futuros;
manifests Kubernetes não fazem parte desta primeira fatia.

## 18. Ordem incremental de implementação

1. solution, dependências direcionais, entidades-base, options, observabilidade,
   health checks e Compose reproduzível;
2. modelo organizacional/profissional e migrations iniciais;
3. usuário, credenciais, sessão SQL, Redis e autenticação completa;
4. autorização por permissão, cache/invalidação e testes fail-closed;
5. auditoria HTTP/entidades e redação de dados sensíveis;
6. contratos, outbox publisher, RabbitMQ e rastreamento distribuído;
7. inbox/consumers, classificação de falhas, retry e DLQ;
8. casos de uso de funcionário e vínculos multi-hospital;
9. testes concorrentes e de integração completos, hardening e pipeline CI/CD;
10. documentação operacional, migration job e preparação de manifests Kubernetes.

Cada incremento deverá compilar, testar suas invariantes e manter a API negada por
padrão. A primeira implementação abaixo limita-se à fundação do item 1 e ao início
do item 2; autenticação e mensageria não serão simuladas parcialmente de forma
insegura.

## 19. Estado após a sétima fatia

Foram implementados o modelo de usuário/perfil/permissão, sessão durável, índice
único filtrado de sessão ativa, lock transacional de login no SQL Server, JWT,
refresh token rotativo, hashes de tokens, login/logout idempotente, validação Redis
com Lua e sliding expiration, cache miss com reidratação SQL, fallback controlado,
checkpoint de atividade, AuditLog e Outbox dos eventos de autenticação. A terceira
fatia adiciona resolução efetiva de permissões por perfil e concessão/negação direta,
catálogo inicial configurável, cache Redis versionado com barreira de invalidação,
métricas, autorização dinâmica ligada ao banco, administração idempotente com lock,
prevenção de autoelevação, AuditLog/Outbox da alteração e endpoints administrativos.
A quarta fatia adiciona `ApiRequestLog`, captura limitada de request/response sem
bufferizar respostas completas, mascaramento recursivo de JSON, form data, query e
headers, associação com usuário/correlação/trace e persistência isolada para não
reutilizar um `DbContext` possivelmente invalidado pela requisição. Falhas ao gravar
a auditoria são registradas de forma estruturada sem substituir a resposta já
produzida.

A quinta fatia consolida o CI em um único workflow fail-fast: restore bloqueado,
formatação, build Release, testes/TRX, auditoria de dependências, script idempotente
de migrations, validação do Compose e build das imagens. A sexta fatia implementa o
publisher da Outbox no Worker com claim atômico SQL (`UPDLOCK`, `READPAST`,
`ROWLOCK`), lease recuperável, backoff exponencial, erro permanente para envelope
inválido, métricas/traces e publicação MassTransit. O contrato é at-least-once: uma
tentativa repetida preserva o mesmo `MessageId`, e a Inbox implementada na sétima
fatia é a barreira idempotente contra efeitos duplicados.

A sétima fatia adiciona `InboxMessages` e `MessageAuditLogs`, chave única por
mensagem/consumer, lock pessimista de linha durante o efeito transacional, consumer
MassTransit durável e métricas próprias. Violações de domínio e validação conhecida
são persistidas como rejeição e recebem ACK. Somente `TransientTechnicalException`
entra no retry exponencial; falhas permanentes, desconhecidas ou com tentativas
esgotadas são persistidas como `DLQ` e encaminhadas à fila `_error` do endpoint. O
histórico permanece no SQL mesmo se RabbitMQ ou suas filas forem perdidos.

A oitava fatia implementa os casos de uso de funcionários: criação, consulta
paginada, detalhe, edição profissional, inativação/reativação e gestão explícita de
unidades de atuação e setores. A unidade de contratação permanece origem imutável,
enquanto as atuações podem abranger hospitais da mesma organização. Vínculos são
encerrados com data e status, nunca excluídos; períodos sobrepostos e duplicidades
ativas são barrados no caso de uso e por índices únicos filtrados. Toda mutação
relevante grava `AuditLog` e evento versionado na Outbox dentro da mesma transação.
