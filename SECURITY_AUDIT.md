# Auditoria estática de segurança

**Projeto:** Sistema de Gestão Empresarial Backend  
**Data da revisão:** 2026-09-03  
**Método:** revisão estática defensiva de código, configuração, infraestrutura e dependências bloqueadas, complementada por testes automatizados e validação dinâmica controlada da pilha Docker; nenhum ataque, fuzzing, DDoS ou acesso a ambiente externo da organização foi realizado.
**Limitações:** esta revisão não substitui teste dinâmico autenticado em staging, revisão do ambiente de produção, SAST/DAST contínuo, geração/revisão contínua de SBOM, revisão das ACLs do host/cloud e teste de restauração.

## Resumo executivo

A aplicação contém bons controles: política de autorização *fallback deny*, permissões verificadas no backend, JWT HS256 com validação de issuer/audience/assinatura/vida útil, access token curto, refresh token aleatório armazenado por hash e rotacionado, revogação de sessão, consultas EF parametrizadas, validação de paginação, limites no Kestrel/Nginx, tratamento global de exceções, imagens da API/worker sem root e pipeline com lock files/NuGet Audit/CodeQL.

Apesar disso, **o perfil Compose fornecido não deve ser publicado na Internet**. A combinação mais grave é Redis sem autenticação/TLS publicado em todas as interfaces, sendo Redis fonte operacional de sessões e permissões. Há ainda SQL Server com conta `sa`, console RabbitMQ e telemetria publicados pelo override, ambiente `Development` aplicado à pilha base, certificado autogerado habilitado por padrão e serviços internos sem TLS. No código, o modelo possui organizações/unidades, mas identidade, permissões e consultas de funcionários são globais: não existe escopo organizacional no JWT nem filtro de tenant. Isso permite acesso entre organizações a qualquer usuário que receba uma permissão funcional global.

**Decisão:** **NO-GO para produção pública** até corrigir SEC-01 a SEC-05 e validar dinamicamente isolamento organizacional, provisionamento inicial de permissões e topologia real de rede/proxy.

## Situação após remediação no repositório

**Atualização em 2026-09-03:** as correções estáticas abaixo foram implementadas e
validadas pela suíte local e por uma pilha Docker isolada no WSL. A decisão continua
**NO-GO para produção pública** até o teste autenticado em staging com dois tenants,
definição de TLS para tráfego interno que atravesse hosts e tratamento
dos registros históricos de auditoria que possam conter PII.

| Achado | Situação | Remediação aplicada / risco residual |
| --- | --- | --- |
| SEC-01 | Mitigado | Redis deixou de ser publicado, recebeu ACL autenticada limitada ao namespace `sge*` e está em rede interna. TLS ainda é obrigatório se Redis sair do mesmo host/rede privada. |
| SEC-02 | Corrigido estaticamente | Escopo é resolvido no servidor por `Usuário → Funcionário → Unidade → Organização`; consultas/mutações de funcionários e gestão de permissões filtram o tenant e negam atores sem vínculo. Foram adicionados testes cross-tenant negativos. |
| SEC-03 | Corrigido estaticamente | Porta SQL removida do override e runtime passou a login dedicado com `SELECT`/`INSERT`/`UPDATE` apenas no schema `sge` e `DELETE`/DDL negados; `sa` fica restrito ao bootstrap/migration. |
| SEC-04 | Mitigado | Management/AMQP não são publicados por padrão, broker usa vhost `/sge` e rede interna. TLS/mTLS permanece requisito para topologias entre hosts. |
| SEC-05 | Corrigido estaticamente | Base usa `Production`, certificado autogerado falha fechado por padrão e overlay produtivo exige certificado/chave montados. Autogeração existe apenas no override/CI. |
| SEC-06 | Mitigado | Auditoria HTTP futura não captura corpos, query, headers, IP ou User-Agent. Dados históricos precisam de política de retenção/expurgo aprovada antes do go-live. |
| SEC-07 | Parcial | Limite ASP.NET usa identidade autenticada ou IP e o Nginx mantém limite por IP. WAF/limite distribuído ainda depende da plataforma de produção. |
| SEC-08 | Corrigido estaticamente | Auditoria usa canal limitado e worker assíncrono; rate limiting e autorização executam antes da captura. Saturação descarta com alerta sem bloquear a requisição. |
| SEC-09 | Corrigido | Criação limita explicitamente unidades de atuação e setores a 50 itens cada. |
| SEC-10 | Corrigido | Bloqueio por cinco falhas passou a ser temporário (15 minutos) e expira automaticamente; migration adicionada. |
| SEC-11 | Corrigido | API e Nginx enviam `Cache-Control: no-store, private` e `Pragma: no-cache`. |
| SEC-12 | Corrigido | Todas as imagens usadas por Dockerfiles, Compose e scripts foram fixadas por digest SHA-256 consultado no registry. |
| SEC-13 | Corrigido | GitHub Actions foram fixadas por commit SHA completo, preservando a versão em comentário. |
| SEC-14 | Mitigado | Foram adicionados limites de CPU/memória/PIDs, `no-new-privileges`, filesystem read-only/tmpfs e `cap_drop` onde compatível. Limites finais devem ser calibrados por carga. |
| SEC-15 | Corrigido | Nginx valida o hostname configurado e usa valor canônico literal no redirect, sem refletir `Host`. `AllowedHosts` é definido pelo deployment. |
| SEC-16 | Corrigido no edge | `/health/ready` retorna 404 no Nginx e só permanece disponível nas redes internas para health checks. |
| SEC-17 | Corrigido | CSP da API/edge usa `default-src 'none'` sem `unsafe-inline`. |
| SEC-18 | Corrigido no Compose | OTLP deixou de ser publicado e sampling produtivo padrão caiu para 10%. Autenticação/TLS do backend externo continua responsabilidade da plataforma. |

**Verificações executadas:** restore locked e NuGet Audit online sem pacote vulnerável
conhecido; build Release sem warnings; `dotnet format --verify-no-changes`; 7 testes
unitários e 51 testes de integração não-real aprovados; `git diff --check` aprovado.
Docker via WSL validou os três manifests, construiu as imagens, iniciou a pilha
completa e aprovou 10 testes `RealInfrastructure`. O Trivy encontrou 20 CVEs High
na imagem Nginx 1.28/Alpine 3.21 originalmente fixada; ela foi substituída por
Nginx 1.30.4/Alpine 3.24 e a nova imagem, API e Worker ficaram com zero achados
High/Critical corrigíveis. A pilha isolada também confirmou Redis anônimo negado,
DELETE/DDL SQL negados ao runtime, readiness 404 no edge e ausência de portas de
dados/admin publicadas.

**Ação operacional pendente:** a pilha preexistente `sistema-gestao-empresarial`,
criada antes destas alterações, permaneceu em execução durante a auditoria e ainda
publicava Redis (`6379`), SQL Server (`11433`), RabbitMQ Management (`15673`) e OTLP
(`4317`/`4318`) em todas as interfaces. Ela não foi interrompida para evitar perda de
estado do ambiente do desenvolvedor. É necessário recriá-la com os manifests novos e
rotacionar as credenciais usadas pela pilha antiga antes de considerar o host seguro.

## Matriz final de risco

| Severidade | Quantidade |
| --- | ---: |
| Critical | 1 |
| High | 5 |
| Medium | 8 |
| Low | 4 |
| Informational | 4 |

## 1. Arquitetura e superfície de ataque

### Fluxo e componentes

```text
Cliente
  -> Nginx :8080 (redirect) / :8443 (TLS, limites por IP/conexão)
    -> API ASP.NET Core :8080 (JWT, sessão Redis/SQL, permissões Redis/SQL, EF Core/SQL Server)
       -> SQL Server (dados, sessões, auditoria, inbox/outbox)
       -> Redis (sessões e cache de permissões)
       -> OTLP Collector (logs, traces e métricas)
Worker ASP.NET Core :8081
  <-> RabbitMQ (eventos; retries e DLQ do MassTransit)
  -> SQL Server / Redis / OTLP
```

Não existe frontend Angular, upload/armazenamento de arquivos, Kubernetes, Dapper, NoSQL, execução de comandos, LDAP/XPath, webhook ou URL fornecida pelo usuário neste repositório. Não foi identificado `HttpClient` de negócio; a instrumentação de HTTP existe, mas não cria uma superfície SSRF por si só.

### Entradas HTTP

| Método e rota | Acesso | Sensibilidade/efeito |
| --- | --- | --- |
| `POST /api/auth/login` | Público; rate limit global + autenticação | Verifica senha, bloqueia conta, cria/revoga sessão, grava auditoria/outbox |
| `POST /api/auth/refresh` | Público; rate limit global + autenticação | Rotaciona tokens; reutilização revoga sessões |
| `POST /api/auth/logout` | JWT/sessão ativa | Revoga todas as sessões ativas do usuário |
| `GET /api/funcionarios` | `FUNCIONARIOS.VISUALIZAR` | Lista paginada; matrícula, nome, e-mail e vínculos |
| `GET /api/funcionarios/{guid}` | `FUNCIONARIOS.VISUALIZAR` | Detalhes e telefone/vínculos de funcionário |
| `POST /api/funcionarios` | `FUNCIONARIOS.CRIAR` | Criação e eventos/auditoria |
| `PUT/PATCH /api/funcionarios/{guid}[/*]` | `FUNCIONARIOS.EDITAR` | Alteração/status/vínculos de unidade e setor |
| `GET/POST/PUT/PATCH /api/profissoes[...]` | Permissão específica de ver/criar/editar | Catálogo profissional |
| `GET/POST/PUT/PATCH /api/cargos[...]` | Permissão específica de ver/criar/editar | Catálogo de cargos |
| `GET /api/niveis-profissionais[/{guid}]` | `NIVEIS_PROFISSIONAIS.VISUALIZAR` | Catálogo de níveis |
| `GET /api/usuarios/{guid}/permissions` | `USUARIOS.PERMISSOES.GERENCIAR` | Enumera permissões efetivas de usuário |
| `PUT /api/usuarios/{guid}/permissions/{code}` | `USUARIOS.PERMISSOES.GERENCIAR` | Concede/nega permissão (ator só concede o que possui) |
| `GET /health/live`, `/health/ready` | Público | Estado da API/dependências |
| `GET /health` | Autenticado | Estado agregado |
| `/swagger`, `/swagger/v1/swagger.json` | Condicional; público quando habilitado | Reconhecimento completo da API |
| `GET /nginx-health` | Público no proxy | Liveness do Nginx |
| Worker `/health/live`, `/health/ready` | Público no processo; não publicado no Compose base | Estado do worker/dependências |

Pontos de entrada não HTTP no perfil Compose: SQL `1433`, Redis `6379`, RabbitMQ Management `15672`, OTLP gRPC `4317` e HTTP `4318` são publicados pelo override; o overlay CI ainda publica AMQP `5672`.

## 2. Achados detalhados

### SEC-01 — Redis sem autenticação publicado permite adulterar estado de segurança

- **Severidade:** 🔴 CRITICAL
- **Arquivo/trecho:** `docker-compose.yml` (`redis-server --appendonly yes`, sem senha/TLS); `docker-compose.override.yml` (`6379:6379`); `DependencyInjection.cs` (Redis alimenta cache e sessão); `RedisSessionOperationalStore.cs` e `PermissionCache.cs`.
- **Componente:** Docker/rede, Redis, autenticação e autorização.
- **Descrição:** o override padrão publica Redis no host sem ACL, senha ou TLS. A aplicação usa esse Redis como estado operacional de sessão e como cache de permissões. As chaves são determinísticas/prefixadas, mas não autenticadas criptograficamente. Uma origem que alcance a porta pode ler/apagar/escrever cache, provocar logout/indisponibilidade e, conforme os valores aceitos pelo cache, forjar permissões operacionais para uma identidade autenticada. O fallback SQL não torna dados Redis hostis confiáveis.
- **Cenário defensivo:** um invasor na rede do host enumera chaves, extrai metadados de sessão/usuário, apaga sessões ou insere uma entrada de permissões compatível com o formato da aplicação. Não é necessário comprometer a API primeiro.
- **Impacto:** escalada de privilégio, bypass de autorização, exposição de metadados, comprometimento de contas e DoS.
- **Evidência:**
  ```yaml
  redis:
    command: ["redis-server", "--appendonly", "yes"]
    ports:
      - "${SGE_REDIS_PORT:-6379}:6379"
  ```
- **Correção recomendada:** remover a publicação; manter Redis somente em rede interna dedicada; exigir Redis ACL com usuário de mínimo privilégio e segredo externo, TLS/mTLS quando atravessar host/rede, firewall/security group e rotação. Tratar cache como não confiável ou assinar/validar estritamente seus valores; separar sessão e permissão em credenciais/namespaces. Não usar o override em produção.
- **Prioridade:** Imediata.

### SEC-02 — Ausência de isolamento organizacional (BOLA/IDOR entre organizações)

- **Severidade:** 🟠 HIGH
- **Arquivo/trecho:** `EmployeeService.cs`, linhas conceituais 20–68 e métodos de mutação; `JwtTokenService.cs`, claims `sub/sid/jti/session_version`; `PermissionAuthorization.cs`.
- **Componente:** autorização, funcionários, multi-organização.
- **Descrição:** a aplicação modela `Organizacao`, `UnidadeHospitalar`, setores e vínculos, porém a identidade não carrega organização/unidade e consultas começam em `dbContext.Funcionarios` sem filtro pelo escopo do ator. Permissões são códigos globais. O GUID apenas identifica o objeto; não autoriza acesso. As validações garantem coerência entre unidades do próprio funcionário, não que o ator possa operar naquela organização.
- **Cenário defensivo:** um usuário autorizado a visualizar ou editar funcionários de uma organização altera `employeeGuid`, `actingUnitGuid`, `relationshipGuid` ou o filtro `actingUnitGuid` para objetos de outra organização. Listagem sem filtro já retorna todas as organizações.
- **Impacto:** vazamento e modificação transversal de PII, escalada horizontal e violação regulatória.
- **Evidência:**
  ```csharp
  var employees = dbContext.Funcionarios.AsNoTracking();
  // ... nenhum OrganizationId derivado do ator ...
  public Task<EmployeeResponse?> GetAsync(Guid employeeGuid, ...) =>
      BuildResponseAsync(employeeGuid, cancellationToken);
  ```
- **Correção recomendada:** definir autorização por recurso e modelo de tenancy; incluir associação organizacional confiável (preferencialmente carregada no servidor, não aceita do request); aplicar filtro obrigatório `OrganizationId` em toda query/mutação e nas tabelas de associação; validar objeto pai e relação no mesmo predicado; usar políticas/resource handlers e testes negativos cruzando dois tenants. Considerar global query filters com salvaguardas, mas não depender apenas deles para operações administrativas.
- **Prioridade:** Imediata.

### SEC-03 — SQL Server administrativo publicado no host

- **Severidade:** 🟠 HIGH
- **Arquivo/trecho:** `docker-compose.yml` (aplicação conecta como `sa`); `docker-compose.override.yml` (`1433:1433`).
- **Componente:** banco de dados e Docker.
- **Descrição:** a API/worker recebem uma connection string com `User Id=sa`, e o override publica SQL Server. Assim, a credencial de administração usada continuamente pela aplicação fica exposta a tentativa de autenticação pela rede e concede controle total do servidor em caso de vazamento.
- **Cenário defensivo:** credential stuffing/brute force na porta publicada ou obtenção do ambiente de um processo/container possibilita login como `sa`, leitura/alteração de dados e destruição do banco.
- **Impacto:** comprometimento integral de dados, persistência e indisponibilidade.
- **Evidência:** `User Id=sa;Password=${SGE_SQLSERVER_SA_PASSWORD...}` e mapeamento `${SGE_SQLSERVER_PORT:-1433}:1433`.
- **Correção recomendada:** não publicar 1433; criar login de aplicação dedicado com direitos mínimos no banco/schema, separar identidade de migração (DDL) da identidade de runtime (DML), armazenar segredo em secret manager e restringir rede. Desabilitar/restringir `sa` conforme política.
- **Prioridade:** Imediata.

### SEC-04 — Console RabbitMQ e canais internos sem TLS

- **Severidade:** 🟠 HIGH
- **Arquivo/trecho:** `docker-compose.override.yml` (`15672:15672`); `docker-compose.ci.yml` (`5672:5672`); `Worker/Program.cs`, configuração de `bus.Host` sem TLS; Redis/OTLP também usam texto claro.
- **Componente:** mensageria e rede interna.
- **Descrição:** o console de administração é publicado no perfil padrão e AMQP no perfil CI, enquanto a conexão do worker não configura TLS. O usuário configurado é o usuário inicial do broker e não há evidência de vhost/permissions mínimos. O console aumenta muito a superfície de administração.
- **Cenário defensivo:** um usuário de rede tenta credenciais no management UI ou captura/reutiliza credenciais em segmento não confiável; com acesso ao broker, injeta mensagens grandes/venenosas, lê filas ou causa esgotamento.
- **Impacto:** comprometimento da mensageria, payloads/auditoria expostos, DoS e eventos forjados.
- **Evidência:** `host.Username(...)`, `host.Password(...)` sem `UseSsl`, e porta management publicada.
- **Correção recomendada:** remover publicações; ativar TLS/mTLS para AMQP, credencial de mínimo privilégio restrita ao vhost/filas, management em rede administrativa/VPN, limites de tamanho/quota e políticas de fila/DLQ. Usar credenciais distintas por produtor/consumidor.
- **Prioridade:** Imediata.

### SEC-05 — Perfil base executa em Development e certificado autogerado por padrão

- **Severidade:** 🟠 HIGH
- **Arquivo/trecho:** `docker-compose.yml` (`DOTNET_ENVIRONMENT: Development` e `NGINX_GENERATE_SELF_SIGNED_CERTIFICATE` default `true`); `appsettings.Development.json` (Swagger habilitado).
- **Componente:** configuração de produção, TLS e Swagger.
- **Descrição:** a configuração base, provável ponto de partida operacional, força Development. Isso habilita Swagger, impede HSTS da aplicação e pode habilitar comportamentos diagnósticos atuais ou futuros. O proxy gera certificado autofirmado se nenhum certificado for montado, tornando fácil implantar TLS não autenticado por engano.
- **Cenário defensivo:** uma pilha promovida sem overlay de produção expõe documentação e usa certificado que clientes precisam ignorar; isso normaliza bypass de validação e facilita MITM.
- **Impacto:** reconhecimento, downgrade de postura, interceptação por configuração operacional indevida.
- **Evidência:** `DOTNET_ENVIRONMENT: Development`, `Swagger.Enabled: true` em Development e `${...:-true}` para geração do certificado.
- **Correção recomendada:** criar manifesto/overlay de produção explícito com `Production`, fail-closed sem certificado confiável, Swagger desabilitado ou autenticado, HSTS no edge, validação automatizada que rejeite Development/self-signed e gestão de certificado ACME/PKI.
- **Prioridade:** Imediata.

### SEC-06 — Auditoria duplica corpos e PII em banco sem política visível de retenção

- **Severidade:** 🟠 HIGH
- **Arquivo/trecho:** `ApiRequestAuditMiddleware.cs`, captura de request/response e persistência; `SensitiveDataRedactor.cs`; DTOs de funcionário.
- **Componente:** logs/auditoria, privacidade e banco.
- **Descrição:** até 64 KiB do request e response JSON são persistidos para todas as chamadas. O redator cobre nomes de segredo, mas não PII como nome, e-mail, telefone, matrícula, IP e User-Agent. Portanto respostas de funcionários e payloads de alteração são duplicados em `ApiRequestLogs`. Não há retenção, criptografia por campo, acesso segregado ou descarte assíncrono visível.
- **Cenário defensivo:** uma conta/backup com acesso apenas a logs obtém histórico de PII; um ataque de alto volume também expande a tabela de auditoria.
- **Impacto:** vazamento de dados pessoais, aumento do raio de impacto e custo/DoS de armazenamento.
- **Evidência:** `requestBody`, `responseBody`, headers, IP e usuário são inseridos em cada `ApiRequestLog`.
- **Correção recomendada:** adotar allowlist de campos/eventos, nunca armazenar resposta completa de endpoints sensíveis, tokenizar/mascarar PII, criptografar com chave gerenciada, RBAC específico, retenção/particionamento/purge, limites de volume e monitoramento. Auditar acesso à própria auditoria.
- **Prioridade:** Alta.

### SEC-07 — Rate limiting por IP é local à instância e não cobre identidade/operação cara

- **Severidade:** 🟡 MEDIUM
- **Arquivo/trecho:** `Program.cs`, `FixedWindowRateLimiter` particionado apenas por `RemoteIpAddress`; `nginx.conf`, limites apenas por IP.
- **Componente:** autenticação, API e DDoS/abuso.
- **Descrição:** o limitador ASP.NET é em memória por réplica; o Nginx é por IP. Não há quota por usuário/token/tenant, limite de concorrência por rota, custo ponderado ou armazenamento distribuído. NAT pode causar negação coletiva e botnets/IPv6 distribuídos contornam limites. Endpoints autenticados de escrita, permissionamento e busca não têm políticas específicas.
- **Cenário defensivo:** múltiplos IPs ou réplicas mantêm cada partição abaixo do limite, gerando consultas, auditoria e outbox; um cliente autenticado consome o limite global sem quota individual útil.
- **Impacto:** resource exhaustion e indisponibilidade; bloqueio de usuários atrás do mesmo NAT.
- **Correção recomendada:** edge/WAF distribuído, limites compostos IP + conta + tenant + endpoint, concurrency limiter, quotas de negócio e budgets para operações caras. Tratar IPv6 por prefixo e monitorar 429/latência. Não considerar rate limiting defesa DDoS volumétrica.
- **Prioridade:** Alta.

### SEC-08 — Auditoria síncrona cria amplificação de banco inclusive em requisições rejeitadas

- **Severidade:** 🟡 MEDIUM
- **Arquivo/trecho:** `ApiRequestAuditMiddleware.cs`, `finally` chama `PersistAsync` e executa lookup de usuário + `SaveChangesAsync`; middleware está antes de exception handler/rate limiter/authentication.
- **Componente:** disponibilidade da API/SQL.
- **Descrição:** cada request que chega ao middleware, incluindo 401, 404 e 429, tenta gravar no SQL e pode esperar até 5 segundos. Isso transforma tráfego barato em I/O persistente e pode manter recursos da API ocupados quando o banco degrada.
- **Cenário defensivo:** requisições anônimas ou bloqueadas em volume provocam inserts e crescimento de log; com SQL lento, cada uma ocupa task/conexão até o timeout.
- **Impacto:** exaustão do pool SQL, disco e latência em cascata.
- **Correção recomendada:** fila/canal limitado assíncrono com política explícita de descarte/amostragem, batch, retenção; excluir rotas/status ruidosos preservando eventos de segurança agregados; colocar proteção barata antes da captura; circuit breaker e métricas de dropped audit events.
- **Prioridade:** Alta.

### SEC-09 — Coleções aninhadas não possuem limite de cardinalidade

- **Severidade:** 🟡 MEDIUM
- **Arquivo/trecho:** `EmployeeValidators.cs`, `RuleForEach` sem `Count` máximo; `EmployeeService.cs`, resolução/loops de unidades/setores; limite total de body é 1 MiB.
- **Componente:** criação de funcionários e banco.
- **Descrição:** há validação individual e distinção, porém nenhum máximo para `ActingUnits` e `Sectors`. Um payload válido até 1 MiB pode conter milhares de GUIDs, gerar listas, `Distinct`, consultas `IN`, loops, inserts e outbox/auditoria volumosos.
- **Cenário defensivo:** usuário com permissão de criação envia coleção muito grande e distinta, amplificando CPU, parâmetros SQL, transação e armazenamento.
- **Impacto:** DoS autenticado e transações longas.
- **Correção recomendada:** limites de domínio pequenos e explícitos (`Count <= N`), tamanho específico por endpoint, limite de parâmetros/tempo/custo, rejeição antecipada e teste de carga seguro.
- **Prioridade:** Alta.

### SEC-10 — Bloqueio de conta pode ser abusado para negação direcionada

- **Severidade:** 🟡 MEDIUM
- **Arquivo/trecho:** `AuthenticationService.cs`, cinco falhas marcam usuário bloqueado; rate limit é apenas por IP.
- **Componente:** autenticação.
- **Descrição:** após cinco tentativas inválidas a conta é bloqueada sem janela temporal/desbloqueio automático visível. Um atacante distribuído que conhece e-mails pode bloquear contas legítimas. A resposta uniforme reduz enumeração direta, e dummy hash é positivo, mas não impede esse abuso.
- **Cenário defensivo:** tentativas espaçadas ou distribuídas contra endereço conhecido atingem o contador, exigindo intervenção operacional.
- **Impacto:** indisponibilidade de contas/administradores e custo de suporte.
- **Correção recomendada:** backoff progressivo temporário, risco adaptativo, limites por conta e IP sem revelar existência, MFA, alertas e fluxo seguro de desbloqueio; evitar bloqueio permanente acionável apenas por terceiros.
- **Prioridade:** Alta.

### SEC-11 — Ausência de `Cache-Control: no-store` em respostas sensíveis

- **Severidade:** 🟡 MEDIUM
- **Arquivo/trecho:** `SecurityHeadersMiddleware.cs` e `nginx.conf`; não há política de cache para API/JWT/PII.
- **Componente:** headers HTTP, autenticação e privacidade.
- **Descrição:** login/refresh retornam tokens e endpoints retornam PII, mas não há `Cache-Control: no-store, private`/`Pragma: no-cache`. Browsers e proxies podem conservar respostas conforme heurísticas/configuração intermediária.
- **Cenário defensivo:** token ou cadastro permanece em cache compartilhado/perfil de navegador e é recuperado por outro usuário/processo.
- **Impacto:** exposição de tokens e PII.
- **Correção recomendada:** middleware/filtro para respostas autenticadas e de auth com `Cache-Control: no-store, private`, `Pragma: no-cache` quando necessário e testes automatizados; não cachear erros que incluam contexto sensível.
- **Prioridade:** Média.

### SEC-12 — Imagens base mutáveis e não fixadas por digest

- **Severidade:** 🟡 MEDIUM
- **Arquivo/trecho:** Dockerfiles (`dotnet/sdk:10.0`, `aspnet:10.0`, `nginx:1.28.0-alpine`) e Compose (`mssql/server:2022-latest`).
- **Componente:** supply chain/container.
- **Descrição:** tags podem apontar para bytes diferentes ao longo do tempo; `2022-latest` é explicitamente flutuante. Builds deixam de ser reproduzíveis e uma alteração upstream comprometida/defeituosa entra sem revisão por digest.
- **Cenário defensivo:** rebuild posterior baixa imagem diferente daquela testada.
- **Impacto:** vulnerabilidade/regressão de supply chain e dificuldade forense.
- **Correção recomendada:** fixar versões patch e digest SHA-256, usar Renovate/Dependabot para atualização revisada, gerar SBOM/proveniência e escanear imagem final (OS + NuGet).
- **Prioridade:** Média.

### SEC-13 — Actions referenciadas por tags mutáveis

- **Severidade:** 🟡 MEDIUM
- **Arquivo/trecho:** `.github/workflows/ci.yml` e `codeql.yml`, por exemplo `actions/checkout@v7` e `github/codeql-action/*@v4`.
- **Componente:** CI/CD.
- **Descrição:** major tags não são referências imutáveis. Embora permissões estejam razoavelmente mínimas e segredos CI sejam efêmeros/mascarados, comprometimento/movimento de tag de action pode executar código no runner.
- **Cenário defensivo:** action upstream alterada recebe acesso ao workspace/token e variáveis de job.
- **Impacto:** supply-chain, adulteração de artefatos e possível exfiltração de credenciais efêmeras.
- **Correção recomendada:** pin por SHA completo com comentário da versão, atualização automatizada revisada, environments protegidos e separar jobs com secrets de análise de PR não confiável.
- **Prioridade:** Média.

### SEC-14 — Sem limites de CPU/memória/PIDs e isolamento adicional nos containers

- **Severidade:** 🟡 MEDIUM
- **Arquivo/trecho:** `docker-compose.yml`; apenas Nginx possui `cap_drop`/`no-new-privileges`; não há limites de recursos.
- **Componente:** Docker/disponibilidade.
- **Descrição:** API e worker rodam como usuário `app`, ponto positivo, mas serviços não possuem quotas de CPU/memória/PIDs, filesystem read-only ou `no-new-privileges`. Uma query, mensagem ou falha com consumo excessivo pode afetar todo o host. Não há Kubernetes; portanto não há requests/limits, NetworkPolicy ou HPA a avaliar.
- **Cenário defensivo:** payload/mensagem válida força memória/CPU; OOM ou contenção derruba serviços vizinhos.
- **Impacto:** indisponibilidade ampliada e pós-exploração facilitada.
- **Correção recomendada:** limites no orquestrador, reservations, pids limit, read-only filesystem + tmpfs, drop de capabilities, `no-new-privileges`, seccomp/AppArmor e redes internas explícitas. Em Kubernetes futuro, definir SecurityContext, quotas, NetworkPolicies e probes.
- **Prioridade:** Média.

### SEC-15 — Redirect HTTP confia no header Host

- **Severidade:** 🔵 LOW
- **Arquivo/trecho:** `nginx.conf`, `return 308 https://$host:8443$request_uri;` com `server_name _`.
- **Componente:** Nginx/headers.
- **Descrição:** cliente controla Host e o servidor catch-all o reflete em `Location`. Isso permite open redirect/host-header injection em links ou clientes que seguem o redirect.
- **Cenário defensivo:** request com Host malicioso recebe redirect para domínio do atacante preservando path/query.
- **Impacto:** phishing e confusão de origem; não produz bypass direto de JWT.
- **Correção recomendada:** configurar nomes permitidos, default server que rejeite hosts desconhecidos e redirect para hostname canônico literal; validar `AllowedHosts` (atualmente `*`).
- **Prioridade:** Baixa.

### SEC-16 — Readiness público revela estado de dependências

- **Severidade:** 🔵 LOW
- **Arquivo/trecho:** `Program.cs`, `/health/ready` anônimo; `HealthResponseWriter.cs`.
- **Componente:** information disclosure.
- **Descrição:** a rota pública permite inferir disponibilidade do SQL/Redis. Mesmo sem detalhes de exception, é um oráculo operacional útil para reconhecimento e sincronização de abuso.
- **Cenário defensivo:** atacante monitora mudanças de estado/restarts e concentra tráfego em recuperação.
- **Impacto:** informação operacional limitada.
- **Correção recomendada:** expor liveness mínimo ao edge; restringir readiness à rede/orquestrador e não retornar nomes/detalhes de dependências publicamente.
- **Prioridade:** Baixa.

### SEC-17 — CSP inclui `unsafe-inline` embora a API não precise executar scripts

- **Severidade:** 🔵 LOW
- **Arquivo/trecho:** `SecurityHeadersMiddleware.cs` e server TLS em `nginx.conf`.
- **Componente:** headers HTTP/Swagger.
- **Descrição:** `script-src/style-src 'unsafe-inline'` enfraquece CSP. Para JSON API, `default-src 'none'` seria suficiente; Swagger deve ter política própria baseada em nonce/hash ou acesso restrito.
- **Cenário defensivo:** se no futuro conteúdo HTML refletido/armazenado for servido nessa origem, inline script terá menos barreiras.
- **Impacto:** redução de defesa em profundidade; nenhum sink XSS atual foi encontrado.
- **Correção recomendada:** CSP restritiva para API e CSP separada sem `unsafe-inline` para documentação; manter `frame-ancestors 'none'`.
- **Prioridade:** Baixa.

### SEC-18 — Telemetria integral e endpoint OTLP publicado ampliam exposição

- **Severidade:** 🔵 LOW
- **Arquivo/trecho:** `appsettings.json` (`SamplingRatio: 1.0`); `DependencyInjection.cs` inclui scopes/formatted message/SQL instrumentation; `docker-compose.override.yml` publica 4317/4318.
- **Componente:** observabilidade.
- **Descrição:** 100% das traces e logs formatados são exportados, o collector recebe em portas publicadas e o arquivo de collector não demonstra autenticação/TLS. Instrumentação pode carregar nomes de host/serviço, rotas e metadados; futuras mensagens mal formuladas podem incluir PII.
- **Cenário defensivo:** acesso de rede ao receiver injeta telemetria e consome recursos; acesso ao backend de observabilidade oferece reconhecimento.
- **Impacto:** log poisoning, DoS e disclosure limitada.
- **Correção recomendada:** não publicar OTLP, autenticar/criptografar fora do host, limitar tamanho/taxa, sanitizar atributos, reduzir sampling em produção e controlar acesso/retention.
- **Prioridade:** Baixa.

### SEC-19 — Controles de autenticação robustos identificados

- **Severidade:** ⚪ INFORMATIONAL
- **Arquivo/trecho:** `Program.cs`, `JwtTokenService.cs`, `AuthenticationService.cs`, `CredentialHasher.cs`.
- **Componente:** JWT/sessão.
- **Descrição/evidência:** valida issuer, audience, assinatura simétrica HS256, lifetime e claims de sessão; access token dura 10 minutos; refresh tem 64 bytes aleatórios, é armazenado por SHA-256, rotaciona e reutilização revoga sessões; logout revoga no SQL/Redis; senha usa `PasswordHasher`; login não distingue usuário inexistente e usa dummy hash. A sessão também é revalidada no backend.
- **Risco residual/recomendação:** chave HS256 precisa ser aleatória, secret manager e rotação com `kid`/janela controlada; SHA-256 de token de alta entropia é adequado. Avaliar MFA, recuperação de senha e bootstrap/admin (não existem neste código), política de senha e eventos de anomalia antes de produção. Guardar tokens no cliente em memória/BFF cookie seguro; o frontend não está presente para validar armazenamento.
- **Prioridade:** Média (validações operacionais).

### SEC-20 — SQL injection, SSRF, path traversal, upload e desserialização insegura não observados

- **Severidade:** ⚪ INFORMATIONAL
- **Arquivo/trecho:** serviços EF Core, `InboxProcessor.cs`, contratos/controllers.
- **Componente:** entrada de dados.
- **Descrição/evidência:** consultas são LINQ/EF ou `FromSqlInterpolated`; `sp_getapplock` usa parâmetro; não há concatenação SQL controlada pelo usuário. Não há entrada URL/host, API HTTP externa, acesso a arquivo fornecido por request ou upload. JSON usa `System.Text.Json`, sem `TypeNameHandling`/tipos arbitrários. Request máximo de 1 MiB e JSON padrão limitam profundidade. Model binding usa DTOs, reduzindo mass assignment.
- **Recomendação:** manter testes SAST, limites por coleção/endpoint, canonicalização e DTOs. Se upload/URL externa forem adicionados, exigir validação de magic bytes/caminho e proteção SSRF incluindo DNS rebinding/redirections/metadata.
- **Prioridade:** Baixa.

### SEC-21 — CORS/CSRF/cookies: não há superfície ativa identificada

- **Severidade:** ⚪ INFORMATIONAL
- **Arquivo/trecho:** `Program.cs` configura somente JWT Bearer e não chama `AddCors`/`UseCors`; não há emissão de cookie.
- **Componente:** navegador/API.
- **Descrição/evidência:** ausência de CORS significa que o browser não concede leitura cross-origin; não é controle de autorização. Como o token precisa ser anexado explicitamente no `Authorization`, CSRF clássico de cookie não se aplica ao fluxo atual. Cookies, SameSite, HttpOnly e antiforgery não são aplicáveis nesta implementação.
- **Recomendação:** se um frontend cross-origin for adicionado, allowlist exata por ambiente, sem refletir Origin. Se migrar token para cookie/BFF, `Secure`, `HttpOnly`, `SameSite`, escopo mínimo e antiforgery/origin checks em todos os métodos mutáveis.
- **Prioridade:** Baixa.

### SEC-22 — Secrets não foram encontrados versionados, mas gestão de produção não está definida

- **Severidade:** ⚪ INFORMATIONAL
- **Arquivo/trecho:** `.env.example`, `appsettings*.json`, Compose e workflows.
- **Componente:** secrets/DevSecOps.
- **Descrição/evidência:** valores versionados estão vazios/placeholders e Compose usa variáveis obrigatórias; CI gera segredos efêmeros e mascara valores. Não há chave privada/certificado/token real rastreado. Porém variáveis de ambiente são legíveis por mecanismos de inspeção privilegiada e não há integração com secret manager/rotação.
- **Recomendação:** secret manager e arquivos/mounts de secret com ACL, identidades workload, rotação, detecção de secrets no pre-commit/CI e histórico Git. Nunca reproduzir segredos em logs/relatórios.
- **Prioridade:** Média (hardening operacional).

## 3. Análises transversais

### Autorização, IDOR e dados sensíveis

A autorização funcional ocorre no backend e a fallback policy autentica endpoints esquecidos. `UsersController` exige permissão de gestão; o serviço repete a checagem e impede autoalteração/concessão de permissão que o ator não possui. Isso é defesa em profundidade positiva. O defeito principal é a ausência de dimensão organizacional (SEC-02). Dados sensíveis identificados: nome, e-mail, telefone, matrícula, vínculo/unidade/setor; hashes de senha/token, metadados de sessão/IP/User-Agent; permissões; payloads inbox/outbox e auditorias.

Não há endpoint de criação de usuário, troca/recuperação de senha ou bootstrap no repositório. Isso impede avaliar com completude provisionamento inicial, password reset, MFA e ciclo de vida de identidade; esses fluxos precisam de revisão antes do go-live.

### Nginx, forwarded headers e IP real

Na topologia Compose declarada, o Nginx **sobrescreve**, em vez de anexar, `X-Forwarded-For` e `X-Real-IP` com `$remote_addr`. ASP.NET habilita `XForwardedFor/Proto/Host`, aceita apenas o IP fixo `172.30.0.2` e limita um hop. A API não publica porta no host e a rede proxy é interna. Assim, **um cliente externo que obrigatoriamente passe por esse Nginx não consegue falsificar o IP usado pela aplicação por simples header spoofing**.

Riscos residuais: IP fixo é frágil em orquestração; exposição direta acidental da API faz o IP ser o socket remoto (headers ignorados), mas contorna os limites Nginx; CDN/load balancer colocado antes do Nginx faria `$remote_addr` virar o proxy, colapsando todos os clientes, a menos que `real_ip_header`, `set_real_ip_from` estrito e `real_ip_recursive` sejam configurados. Não se deve aceitar ranges amplos nem `$proxy_add_x_forwarded_for` sem análise da cadeia.

O Nginx possui bons limites de body/timeouts/conexões, buffering, TLS 1.2/1.3 e `server_tokens off`. Faltam limites distribuídos/upstream, hostname canônico, certificado produtivo e tratamento explícito de proxy/CDN. Portanto está razoável para desenvolvimento, **não corretamente pronto para produção**.

### Banco, concorrência e mensageria

EF gera SQL parametrizado; SQL interpolado do inbox parametriza valores. Transações e locks de aplicação tratam várias corridas; índices/constraints e testes de concorrência reduzem duplicação. A API não implementa chave de idempotência para POST: retries do cliente podem criar duas entidades distintas quando a unicidade de e-mail/nome não bloquear, devendo ser avaliado como desenho de negócio. Inbox usa `(MessageId, Consumer)` e lock; outbox dá durabilidade. Mensagens/payloads completos ficam no SQL; limitar tamanho no broker e retenção continua necessário.

### Erros e information disclosure

O handler global devolve título genérico/correlation ID e registra detalhes no servidor; não envia stack trace no DTO. Swagger só depende de flag, sem autenticação. `Server` upstream é ocultado e headers defensivos existem. Logs de exceptions podem conter mensagens do SQL/infra; controle de acesso e redaction no backend de observabilidade são necessários. Campos controlados pelo usuário são estruturados na maioria dos logs, reduzindo, mas não eliminando, log forging em renderizadores/exportadores.

### Dependências

Projetos usam lock files, restore locked, NuGet Audit com advisories como erro e CodeQL. Isso é positivo. A validade de CVEs é temporal: executar `dotnet list ... --vulnerable --include-transitive`, scanner de imagens e consultar advisories oficiais no pipeline/release. Esta auditoria não atribui CVE sem resultado verificável. Merecem validação contínua: ASP.NET/EF/IdentityModel 10.0.11/8.22.0, StackExchange.Redis 3.1.31, MassTransit 8.5.8, OpenTelemetry 1.18.0, Swashbuckle 10.2.3 e health checks 9.0.0, além das imagens Nginx/Redis/RabbitMQ/SQL/OTel.

## 4. Resistência a abuso — rotas prioritárias

| Risco | Endpoint/entrada | Motivo |
| --- | --- | --- |
| Muito alto | Redis/SQL/Rabbit management publicados | Ataca infraestrutura diretamente, fora dos limites HTTP |
| Alto | `POST /api/auth/login` | hash de senha + transação + lock + auditoria/outbox; bloqueio direcionado |
| Alto | `POST /api/funcionarios` e vínculos | coleções, resolução de referências, transação, múltiplos inserts, auditoria/outbox |
| Alto | alterações de permissão | locks distribuídos, cache, SQL, revogação prática de acesso |
| Médio-alto | qualquer request inválido/401/429 | insert síncrono de auditoria |
| Médio | listagem/busca de funcionários | `Contains`, count + page, PII e possível scan; page size máximo 100 é positivo |
| Médio | refresh | SQL + lock + rotação Redis; limite específico existe |
| Baixo | health/readiness | consultas às dependências, públicas e fáceis de automatizar |

Não há exportação/relatório/e-mail/SMS/upload/API externa. O limite de 1 MiB, timeouts e paginação máxima de 100 são bons controles, mas não substituem proteção volumétrica upstream e quotas por identidade.

## 5. Revisão OWASP

### OWASP Top 10 (2021)

| Categoria | Resultado |
| --- | --- |
| A01 Broken Access Control | **Afetado:** SEC-01/SEC-02; controles funcionais positivos, tenancy ausente |
| A02 Cryptographic Failures | **Afetado:** SEC-04/SEC-05; JWT/senhas/tokens bem desenhados no código |
| A03 Injection | Sem SQL/command/LDAP/XPath/template injection identificado; risco de telemetria/log reduzido mas residual |
| A04 Insecure Design | **Afetado:** tenancy, account lockout, auditoria síncrona, idempotência HTTP ausente |
| A05 Security Misconfiguration | **Afetado:** serviços publicados, Development/self-signed, cache headers/readiness |
| A06 Vulnerable/Outdated Components | Processo bom; imagens mutáveis e validação temporal necessária |
| A07 Identification/Auth Failures | **Afetado:** SEC-10; JWT/refresh/revogação fortes |
| A08 Software/Data Integrity Failures | **Afetado:** actions/imagens sem SHA/digest; inbox/outbox idempotentes são positivos |
| A09 Logging/Monitoring Failures | Logging amplo; **excesso de logging/PII**, retenção e proteção não demonstradas |
| A10 SSRF | Não aplicável ao código atual; sem URL/HttpClient controlado pelo usuário |

### OWASP API Security Top 10 (2023)

| Categoria | Resultado |
| --- | --- |
| API1 BOLA | **Afetado:** SEC-02 |
| API2 Broken Authentication | Parcial: lockout abusável; tokens/sessões robustos |
| API3 Broken Object Property Level Authorization | DTOs evitam mass assignment; escopo organizacional ainda falha |
| API4 Unrestricted Resource Consumption | **Afetado:** SEC-07/08/09/14 |
| API5 Broken Function Level Authorization | Permissões backend/fallback positivos; adulteração Redis quebra a garantia |
| API6 Unrestricted Access to Sensitive Business Flows | **Afetado:** autenticação/bloqueio e mutações sem quota/idempotency key |
| API7 SSRF | Não identificada |
| API8 Security Misconfiguration | **Afetado:** SEC-01/03/04/05/11/15/16 |
| API9 Improper Inventory Management | Superfície pequena/documentada; Swagger Development e ausência de inventário por ambiente são residuais |
| API10 Unsafe Consumption of APIs | Sem HTTP API externa; consumo Rabbit precisa TLS, ACL e limites |

## 6. TOP 10 problemas mais perigosos

1. **Redis público sem autenticação, controlando sessão/permissão** (Critical).
2. **BOLA/IDOR entre organizações por falta de escopo organizacional** (High).
3. **SQL Server público e aplicação usando `sa`** (High).
4. **RabbitMQ Management/AMQP exposto e tráfego sem TLS/ACL demonstrada** (High).
5. **Compose base em Development com TLS autofirmado por padrão** (High).
6. **PII e corpos completos duplicados em auditoria sem retenção/segregação** (High).
7. **Rate limiting apenas por IP/local, sem quota por conta/tenant/rota** (Medium).
8. **Write amplification síncrona da auditoria em toda requisição** (Medium).
9. **Coleções de vínculos sem cardinalidade máxima** (Medium).
10. **Bloqueio permanente de conta acionável por tentativas distribuídas** (Medium).

## 7. Avaliação específica de produção

1. **Está segura para Internet?** Não, não com os manifestos fornecidos.
2. **Há bloqueadores de deploy?** Sim: SEC-01 a SEC-05 e validação do isolamento organizacional.
3. **Risco relevante de vazamento?** Sim: acesso cross-organization, infraestrutura publicada e duplicação de PII em auditoria.
4. **Risco de acesso não autorizado?** Sim: adulteração do cache Redis e BOLA multi-organização.
5. **Risco de DDoS/resource exhaustion?** Sim: infraestrutura publicada, auditoria síncrona, limites não distribuídos, coleções e ausência de quotas de container.
6. **Rate limiting suficiente?** Não para produção pública; é boa camada local, não proteção completa contra botnet/DDoS/abuso autenticado.
7. **Nginx correto?** Parcialmente hardenizado, mas não pronto para produção pelos itens de certificado/Host/topologia/limites distribuídos.
8. **IP real falsificável?** Na topologia Compose exata e sem proxy anterior, não por headers externos. Pode ficar incorreto/colapsado se houver CDN/LB anterior ou mudança do proxy conhecido; exige revalidação no ambiente.
9. **Secrets expostos?** Nenhum segredo real versionado foi identificado; portas e uso de env/`sa` elevam o impacto de qualquer vazamento. Gestão produtiva não está definida.
10. **Endpoints públicos indevidos?** Readiness e Swagger quando Development poderiam ser restritos; login/refresh/liveness são legitimamente públicos.
11. **Endpoints autenticados abusáveis?** Sim: criação/vínculos, permissões, buscas e qualquer rota por amplificação de auditoria.
12. **Critical/High imediatos?** Sim: SEC-01 a SEC-06, especialmente Redis, tenancy, SQL/Rabbit e perfil Development/TLS.

## 8. Plano recomendado

**0–48 horas:** retirar todas as portas de dados/admin do host; rotacionar credenciais caso a pilha já tenha sido exposta; isolar Redis e habilitar ACL/TLS; trocar runtime `sa`; implantar Production com certificado confiável; restringir Rabbit/OTLP; bloquear release até teste cross-tenant.

**Primeira sprint:** implementar tenancy/resource authorization e testes negativos; reduzir/proteger auditoria; quotas por identidade/endpoint e limites de coleção; cache-control; políticas de conta/MFA; pin de imagens/actions.

**Antes do go-live:** DAST autenticado com dois tenants e papéis, teste de abuso não destrutivo em staging, revisão cloud/firewall/secrets/TLS, threat model, SBOM e scans de imagens/dependências, teste de backup/restore, alertas/rate dashboards e runbooks de revogação/incidente.
