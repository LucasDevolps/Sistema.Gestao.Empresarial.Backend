# Arquitetura do sistema

> **Documento _as built_.** Validado em 10 de setembro de 2026 contra o código,
> migrations, testes, arquivos Compose e workflows existentes no repositório. As
> seções abaixo descrevem o estado implementado; intenções futuras aparecem somente
> na seção [Limites atuais e evolução](#16-limites-atuais-e-evolução).

## 1. Visão geral

O backend é uma aplicação hospitalar multi-organização construída em .NET 10,
ASP.NET Core, Entity Framework Core e SQL Server. A solução também usa Redis para
estado operacional de sessão e cache de permissões, RabbitMQ/MassTransit para
mensageria e OpenTelemetry para observabilidade.

A implementação segue uma separação inspirada em Clean Architecture:

```text
Domain <----- Application <----- Api
   ^              ^               |
   |              |               v
   +---------- Infrastructure <----+
                  ^
                  |
             Worker / Bootstrap

AppHost --orquestra em desenvolvimento--> Api, Worker e dependências

UnitTests --------> Domain / Application
IntegrationTests -> Api / Infrastructure (e dependências transitivas)
```

As referências de projeto preservam `Domain` sem dependências das camadas externas
e `Application` dependente apenas de `Domain`. `Infrastructure` implementa os
contratos da aplicação. `Api`, `Worker` e `Bootstrap` são composition roots e
referenciam `Application` e `Infrastructure`.

## 2. Projetos da solution

- `Sistema.Gestao.Empresarial.Domain`: entidades, invariantes e exceções de domínio;
- `Sistema.Gestao.Empresarial.Application`: contratos de casos de uso, DTOs,
  validação e envelopes de integração;
- `Sistema.Gestao.Empresarial.Infrastructure`: EF Core/SQL Server, autenticação,
  Redis, autorização, auditoria, Outbox, Inbox, MassTransit e implementações dos
  casos de uso;
- `Sistema.Gestao.Empresarial.Api`: controllers, pipeline HTTP, JWT, autorização,
  rate limiting, Swagger e health checks;
- `Sistema.Gestao.Empresarial.Worker`: publicação da Outbox, consumo da Inbox,
  retenção de auditoria e endpoints de health;
- `Sistema.Gestao.Empresarial.Bootstrap`: executável one-shot para provisionar o
  primeiro administrador;
- `Sistema.Gestao.Empresarial.AppHost`: orquestração local com .NET Aspire;
- `Sistema.Gestao.Empresarial.UnitTests`: testes rápidos de `Domain` e `Application`;
- `Sistema.Gestao.Empresarial.IntegrationTests`: testes da API, infraestrutura,
  arquitetura e uma categoria opt-in `RealInfrastructure`.

Controllers funcionam como adaptadores HTTP finos: validam o contrato de entrada,
delegam aos serviços e convertem o resultado em resposta HTTP. Regras e acesso a
dados não ficam nos controllers.

## 3. Execução e containers

O deployment Compose principal tem a seguinte topologia:

```text
                         cliente HTTPS
                                |
                    Nginx / TLS / limites
                                |
                              API
                    +-----------+-----------+
                    |           |           |
               SQL Server     Redis      RabbitMQ
                                            |
                                          Worker

                 API + Worker --OTLP--> OTel Collector
                                             |
                                      Aspire Dashboard
                                      (somente override dev)
```

Nginx é a borda pública da API. Há redes distintas para borda, proxy da API e
serviços internos. SQL Server, Redis e RabbitMQ usam volumes persistentes e health
checks; o serviço `sqlserver-init` cria o login de aplicação com privilégio
reduzido. API e Worker possuem Dockerfiles multi-stage, executam com o usuário
não-root `app` e recebem configuração por variáveis de ambiente.

O `docker-compose.override.yml` de desenvolvimento publica Nginx somente em
loopback e adiciona Aspire Dashboard e Collector. O arquivo de produção não inclui
o override de desenvolvimento. Migrations são aplicadas como uma etapa explícita,
fora das réplicas da API e do Worker.

O repositório também contém uma topologia opcional de réplica síncrona do SQL
Server e automações de backup/verificação. Esses procedimentos e suas limitações
estão detalhados em [`backup-and-replication.md`](backup-and-replication.md).

## 4. Modelo organizacional e multi-hospital

```text
Organizacao 1 --- N UnidadeHospitalar 1 --- N Setor N --- 1 CategoriaSetor
                           |                    |
                           |                    +--- N SetorUnidadeAtendida N --- 1 UnidadeHospitalar
                           |
                           +--- N FuncionarioUnidadeAtuacao N --- 1 Funcionario
                           |                                      |
                           +--- unidade de contratação -----------+

Funcionario 1 --- N FuncionarioSetor N --- 1 Setor
```

`UnidadeContratacaoId` registra a origem contratual do funcionário. O escopo de
atuação é representado separadamente por `FuncionarioUnidadeAtuacao`, com período e
status. A associação `FuncionarioSetor` liga o funcionário aos setores em que atua.

O `Setor` pertence a uma unidade principal imutável, é classificado por
`CategoriaSetor` (catálogo global configurável, como `Profissao`/`Cargo`) e, quando
permite atuação compartilhada, atende unidades adicionais por
`SetorUnidadeAtendida` — vínculo temporal com período e status, encerrado nunca
removido. Nome e sigla são únicos por unidade principal (índices filtrados a
registros não excluídos, collation `Latin1_General_CI_AS`).

Organização, unidade, setor, categoria e vínculos são entidades configuráveis, não
enums. Os casos de uso restringem vínculos à organização da unidade de contratação.
Leituras de identidade e catálogos derivam o tenant pela cadeia
`Usuario → Funcionario → UnidadeContratacao → Organizacao` e filtram outra
organização antes da projeção.

## 5. Profissão, cargo, nível e funcionário

- `Profissao`: catálogo com nome, descrição e ciclo de vida;
- `Cargo`: catálogo com profissão compatível opcional e ciclo de vida;
- `NivelProfissional`: catálogo ordenado por campo estrutural `Ordem`;
- `Funcionario`: matrícula, dados pessoais/profissionais, profissão, cargo, nível e
  unidade de contratação;
- `FuncionarioUnidadeAtuacao` e `FuncionarioSetor`: vínculos operacionais.

Entidades usam `long` como chave interna e `Guid` como identidade pública. A
matrícula é gerada no SQL Server por `SEQUENCE`. Os endpoints não expõem IDs
internos. A API implementa criação, consulta paginada, detalhe, edição profissional,
inativação/reativação e gestão dos vínculos do funcionário, além de leitura e
manutenção dos catálogos profissionais.

Chaves de negócio textuais relevantes usam collation _case-insensitive_ no banco e
possuem constraints/índices para manter unicidade também sob concorrência.

## 6. Persistência, exclusão lógica e retenção

Entidades de negócio persistentes derivam dos tipos-base auditáveis e, quando
aplicável, possuem `Ativo`, `Excluido`, `ExcluidoEm` e `ExcluidoPor`. O `AppDbContext`
aplica filtros globais a entidades soft-deletable. Operações de negócio inativam ou
encerram registros e vínculos, em vez de removê-los fisicamente.

O contexto rejeita entradas EF no estado `Deleted`, protegendo o caminho normal de
persistência contra exclusão física acidental. Há, porém, uma exceção operacional
deliberada: `AuditRetentionWorker` usa `ExecuteDeleteAsync` diretamente para expurgar
em lotes `ApiRequestLogs` e `AuditLogs` vencidos. Por padrão, logs HTTP são mantidos
por 180 dias e auditorias de negócio por 1.825 dias; habilitação, prazos, lote,
frequência e limite por varredura são configuráveis em `AuditRetention`.

`OutboxMessages`, `InboxMessages` e `MessageAuditLogs` não participam hoje desse
expurgo. Qualquer política futura para essas tabelas precisa preservar os requisitos
de rastreabilidade e idempotência.

## 7. Autenticação, JWT e sessão única

`Usuario` e `Funcionario` são agregados distintos, com relação opcional. Senhas são
armazenadas como hash. O login protege contra enumeração de usuários, aplica bloqueio
temporário configurável após falhas e não registra credenciais em claro.

O access token JWT contém `sub`, `sid`, `jti` e `session_version`, tem validade curta
(10 minutos por padrão) e usa chave simétrica fornecida por configuração. O refresh
token é opaco; somente seu hash é persistido e ele é rotacionado a cada uso. Reuso
de token substituído revoga a sessão.

O SQL Server é a fonte durável e auditável da sessão; Redis mantém o estado
operacional de baixa latência. O login é serializado por usuário com
`sp_getapplock`, revoga sessões anteriores, incrementa a versão da sessão, persiste
sessão/auditoria/Outbox na transação e atualiza o estado operacional. Um índice
filtrado reforça a unicidade da sessão ativa.

Logout é idempotente. A validação de cada JWT compara usuário, sessão, `jti`, versão,
status e atividade. O limite de inatividade é de 30 minutos; existe ainda uma vida
absoluta configurável (sete dias por padrão).

## 8. Redis e fallback de sessão

Chaves Redis são centralizadas em `IRedisKeyFactory` e usam prefixo de instância e
ambiente. O armazenamento operacional mantém sessão ativa, ponteiro do usuário,
snapshot de permissões e atividade pendente de checkpoint.

Scripts Lua realizam validação e atualização atômicas do estado de sessão e das
permissões. Em cache miss, a sessão pode ser reidratada a partir do SQL. Se Redis
estiver indisponível, o fallback para SQL é controlado por
`Session:EnableSqlFallback`; falhas que não podem ser validadas com segurança são
negadas. Quando o script indica que o intervalo configurado foi atingido, a própria
validação da sessão consolida no SQL a atividade registrada no Redis.

## 9. Autorização configurável e fail-closed

O modelo contém `Perfil`, `Permissao`, `PerfilPermissao`, `UsuarioPerfil` e
`UsuarioPermissao`. Permissões diretas podem conceder ou negar acesso, e a negação
direta tem precedência na resolução efetiva.

A API usa policies dinâmicas e `RequirePermissionAttribute`. A fallback policy exige
autenticação, enquanto endpoints públicos precisam declarar `AllowAnonymous`. O
cache Redis de permissões é versionado; alterações instalam uma barreira de
invalidação para impedir que uma requisição aceite um snapshot antigo.

Os endpoints administrativos permitem gerir permissões com auditoria e Outbox,
lock lógico e prevenção de autoelevação. Testes arquiteturais enumeram endpoints
para verificar que cada um possui permissão/policy ou exposição pública explícita.

## 10. API HTTP e segurança de borda

A API expõe controllers para:

- autenticação (`login`, `refresh` e `logout`);
- identidade atual e administração de usuários/permissões;
- funcionários e seus vínculos de unidade/setor;
- profissões, cargos e níveis profissionais;
- organizações, unidades hospitalares e setores.

`ProblemDetails` e um exception handler centralizam erros. Swagger possui esquema
Bearer, é habilitado por configuração e fica desligado na configuração padrão.
Kestrel aplica limite de body de 1 MiB, timeouts e taxa mínima. O rate limiter tem
uma política global por usuário/IP e uma política mais restrita para autenticação.

Quando `ReverseProxy:Enabled=true`, somente proxies explicitamente conhecidos são
aceitos, com `ForwardLimit=1`. Nginx termina TLS, substitui forwarded headers, aplica
limites de conexão/taxa/body e adiciona headers de segurança. A própria API também
adiciona headers de segurança e HSTS fora de Development.

## 11. Auditoria

O middleware HTTP cria ou normaliza `X-Correlation-ID`, preserva `TraceId`, limita a
captura de request/response e mascara headers, query string, JSON e form data
sensíveis. `ApiRequestLog` é persistido por um canal limitado e por um escopo de
`DbContext` separado do processamento da requisição; falha de auditoria não troca a
resposta HTTP já produzida.

`AuditLog` registra mudanças de negócio com ator, origem, correlação, trace e estado
antes/depois. Nos casos de uso transacionais, mudança de domínio, auditoria e Outbox
são gravadas na mesma transação. A política de retenção física é a exceção descrita
na seção 6.

## 12. RabbitMQ, Outbox e Inbox

Eventos de integração usam DTOs em envelope versionado, não entidades EF. O envelope
carrega identificadores de evento/mensagem, tipo, versão, correlação, contexto de
trace, instante UTC, produtor, ator e payload.

O Worker disputa lotes da Outbox no SQL Server usando claim atômico com lease,
`UPDLOCK`, `READPAST` e `ROWLOCK`. Publicações inválidas são classificadas como erro
permanente; falhas transitórias recebem backoff. A entrega é _at least once_ e
preserva o mesmo `MessageId` entre tentativas.

O consumer MassTransit usa endpoint durável e Inbox com chave única por
`(MessageId, Consumer)`. Duplicatas já processadas recebem ACK sem repetir o efeito.
Violações conhecidas de domínio/validação são auditadas como rejeição e recebem ACK.
Somente `TransientTechnicalException` entra no retry exponencial; falhas permanentes,
desconhecidas ou esgotadas são persistidas como DLQ e seguem para a fila `_error`.

## 13. Observabilidade e health checks

API e Worker exportam logs, traces e métricas via OTLP. A instrumentação inclui
ASP.NET Core, HttpClient, SqlClient, runtime e medidores próprios de autenticação,
permissões, Outbox e Inbox. Resources incluem nome, versão, ambiente e instância do
serviço. A razão de sampling é configurável.

Não há dependência de fornecedor dentro das regras de negócio. O Collector é o
ponto de configuração dos exporters. No ambiente Compose de desenvolvimento ele
encaminha sinais ao Aspire Dashboard.

Nos dois executáveis:

- `/health/live` é público e verifica apenas o processo;
- `/health/ready` é público para orquestração e verifica dependências registradas;
- na API, `/health` executa todas as verificações e exige autenticação.

A readiness da API verifica SQL Server e Redis. A do Worker verifica SQL Server,
Redis e também o bus RabbitMQ registrado pelo MassTransit.

## 14. Bootstrap administrativo

`Sistema.Gestao.Empresarial.Bootstrap` é um comando one-shot, não um endpoint da
API. Ele lê a senha de um arquivo, valida configuração explícita e recusa execução
se qualquer usuário, inclusive soft-deleted, já existir.

No SQL Server, uma transação serializável e `sp_getapplock` impedem bootstraps
concorrentes. Organização, unidade, catálogos mínimos, funcionário, usuário, perfil,
permissões, auditoria e Outbox são persistidos atomicamente. O script
`bootstrap-initial-admin-docker.sh` integra esse fluxo ao ambiente Compose.

## 15. Testes e entrega contínua

O workflow principal executa restore em modo locked, verificação de formatação,
build Release, testes rápidos com cobertura, auditoria de dependências, geração de
script idempotente de migration, validação do Compose e build das imagens.

O mesmo workflow sobe SQL Server, Redis e RabbitMQ com credenciais efêmeras e roda a
categoria `RealInfrastructure`. Essa suíte cobre concorrência e constraints no SQL,
autoridade de sessão no Redis, Inbox/Outbox, retry/DLQ e integração transportada pelo
RabbitMQ. Ao final, o CI aplica migrations, sobe API/Worker/Nginx e executa um smoke
test HTTPS. Imagens e filesystem também são verificados pelo Trivy, e há workflow
separado de CodeQL.

## 16. Limites atuais e evolução

- Kubernetes ainda não possui manifests neste repositório; Compose é a definição
  executável de deployment atual e Aspire é exclusivo para desenvolvimento local.
- O sistema usa assinatura JWT simétrica. Rotação sem indisponibilidade exige
  evolução para múltiplas chaves identificadas (por exemplo, `kid`) ou provedor de
  identidade externo.
- O modelo e os casos de uso implementados cobrem identidade, autorização,
  organização, catálogos e funcionários. Pacientes, atendimentos, prescrições e
  escalas ainda são módulos futuros.
- Retenção automatizada existe para `ApiRequestLogs` e `AuditLogs`, mas não para
  tabelas de Inbox, Outbox e auditoria de mensagens.
- A réplica SQL e os backups são recursos operacionais opcionais; não substituem um
  plano externo de recuperação de desastre nem estão ativos sem configuração.

## 17. Orquestração local com .NET Aspire

O `Sistema.Gestao.Empresarial.AppHost` modela somente o ambiente de desenvolvimento.
Ele inicia SQL Server, Redis e RabbitMQ em containers, executa inicialização do login
SQL e migrations como jobs one-shot e então inicia API e Worker como projetos .NET.
Health checks autenticados e dependências de startup ficam visíveis no Dashboard.

Não existe projeto `ServiceDefaults`: observabilidade e health checks são
registrados pela própria aplicação. O AppHost fornece o endpoint OTLP temporário do
Dashboard à configuração existente em Development, sem adicionar captura de
headers, corpos, query strings, tokens ou PII.

Aspire não é artefato de produção. Compose, Nginx, Collector, redes, imagens fixadas
por digest e controles de hardening continuam sendo a fonte de verdade operacional.
