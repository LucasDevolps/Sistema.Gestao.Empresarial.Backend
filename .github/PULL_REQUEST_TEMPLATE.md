## Descrição e escopo

<!-- Explique o problema, o que mudou e por quê. Delimite o escopo e o comportamento antes/depois. -->

## Tipo de alteração

- [ ] Correção de bug
- [ ] Nova funcionalidade
- [ ] Refatoração
- [ ] Segurança
- [ ] Performance
- [ ] Banco de dados
- [ ] Infraestrutura / DevOps
- [ ] Documentação
- [ ] Testes
- [ ] Breaking change

Issue / card relacionado (opcional): <!-- Ex.: Closes #123; remova se não houver. -->

## Segurança

- [ ] Revisei o diff e os anexos para evitar secrets, tokens, senhas, connection strings, credenciais e dados pessoais/hospitalares reais.
- [ ] Avaliei os impactos de segurança e preenchi as revisões aplicáveis abaixo, incluindo permissões e isolamento no backend.

<!-- Siga SECURITY.md para divulgação responsável. Não publique exploits perigosos nem detalhes sensíveis de reprodução. -->

## Testes e validação

<!-- Informe comandos, resultados e limitações. Justifique testes/build não executados ou a ausência de novos testes (ex.: documentação). Não marque verificações pendentes como concluídas. -->

- [ ] Executei build e testes pertinentes; registrei os resultados e eventuais testes ignorados.
- [ ] Adicionei/atualizei testes para o comportamento alterado, incluindo erros, autorização negada, isolamento e concorrência quando afetados.
- [ ] Revisei formatação, warnings e análises aplicáveis do CI: NuGet Audit, CodeQL (C# e Actions), Trivy e geração de SBOM.

Comandos e resultados:

<!-- Comandos existentes no README/CI, conforme o escopo:
dotnet tool restore
dotnet restore Sistema.Gestao.Empresarial.sln --locked-mode
dotnet format Sistema.Gestao.Empresarial.sln --no-restore --verify-no-changes
dotnet build Sistema.Gestao.Empresarial.sln --configuration Release --no-restore
dotnet test Sistema.Gestao.Empresarial.sln --configuration Release --no-build --no-restore --filter "Category!=RealInfrastructure"
Para RealInfrastructure com SQL Server, Redis e RabbitMQ: siga README.md e scripts/run-real-integration-tests-wsl.sh.
A suíte exige SGE_REAL_INFRASTRUCTURE_TESTS=true e configuração das dependências; testes ignorados não comprovam integração real.
-->

## Revisões específicas — quando aplicável

Informe quais seções abaixo se aplicam. Para as demais, escreva “não aplicável” e uma justificativa breve (pode agrupar seções). Marque somente itens efetivamente revisados. Este checklist auxilia a revisão; não certifica segurança nem bloqueia o merge automaticamente.

Aplicabilidade / justificativas:

<details>
<summary>Arquitetura e API / contratos</summary>

- [ ] Preservei os limites Domain → regras de domínio, Application → casos de uso/DTOs/FluentValidation, Infrastructure → persistência/integrações e Api/Worker → composição; Domain permanece sem dependências de infraestrutura.
- [ ] Revisei DTOs, validação de entrada, paginação limitada, identificadores públicos `Guid` e respostas HTTP/ProblemDetails, sem expor IDs internos ou detalhes de exceções.
- [ ] Avaliei compatibilidade dos endpoints/contratos; documentei abaixo qualquer quebra e a adaptação necessária dos consumidores.

</details>

<details>
<summary>Autenticação, autorização e sessão</summary>

- [ ] Revisei policies e `RequirePermission`, negação por padrão e justificativa de `AllowAnonymous`; permissões são verificadas no backend, com proteção contra autoelevação.
- [ ] Validei JWT e claims de sessão (`sub`, `sid`, `jti`, `session_version`), rotação/reuso de refresh, logout/revogação, sessão única e expiração por inatividade quando afetados.
- [ ] Revisei rate limiting global/login/refresh, bloqueio de tentativas e respostas de erro para evitar bypass ou enumeração de usuários.

</details>

<details>
<summary>Multi-hospital / isolamento de dados</summary>

- [ ] Revisei consultas e mutações pelo escopo organizacional derivado do usuário autenticado; filtros do frontend ou identificadores fornecidos pelo cliente não concedem acesso.
- [ ] Considerei organizações distintas e funcionários com múltiplas unidades de atuação/setores; a unidade de contratação não equivale a uma permissão de acesso.
- [ ] Preservei a coerência entre organização, unidade hospitalar e setor, incluindo histórico e encerramento dos vínculos.

</details>

<details>
<summary>Banco de dados — EF Core / SQL Server</summary>

- [ ] Revisei migrations e sua necessidade, script idempotente, compatibilidade com dados existentes, aplicação controlada e estratégia de recuperação/rollback descrita abaixo.
- [ ] Avaliei queries, índices/constraints e concorrência (`rowversion`, unicidade e locks quando usados), preservando exclusão lógica e filtros existentes.
- [ ] Preservei a atomicidade de alterações de domínio, auditoria e Outbox quando participam da mesma transação.

</details>

<details>
<summary>Mensageria — RabbitMQ / MassTransit</summary>

- [ ] Revisei versões/compatibilidade dos envelopes e eventos, sem incluir entidades EF ou dados sensíveis desnecessários.
- [ ] Considerei publicação ao menos uma vez pela Outbox, `MessageId` estável e deduplicação da Inbox por mensagem/consumer.
- [ ] Revisei retries de falhas transitórias, rejeições de negócio/validação, encaminhamento à fila `_error`/DLQ e preservação do histórico.

</details>

<details>
<summary>Cache / Redis</summary>

- [ ] Revisei chaves via `IRedisKeyFactory`, escopo por ambiente/usuário/sessão, TTL e ausência de tokens/segredos em claro.
- [ ] Validei invalidação e versionamento de sessões/permissões, operações atômicas e comportamento em cache miss/indisponibilidade, respeitando o fallback SQL configurado e a negação em falhas ambíguas.

</details>

<details>
<summary>Observabilidade e auditoria</summary>

- [ ] Avaliei logs, métricas e traces OpenTelemetry necessários e propagação de `CorrelationId`/`TraceId`, sem adicionar coleta desnecessária ou labels de alta cardinalidade.
- [ ] Preservei a auditoria HTTP de metadados mínimos, sem captura de corpos, headers ou query strings sensíveis; revisei também eventos, erros e anexos para evitar vazamento de dados.

</details>

<details>
<summary>Infraestrutura e dependências</summary>

- [ ] Validei Docker/Compose afetados, health checks, usuário não root da API/Worker e hardening de produção; avaliei dependências NuGet/imagens/Actions e seus lockfiles ou referências fixadas.
- [ ] Revisei TLS/headers/limites do Nginx e proxy confiável/IP real da API; SQL Server, Redis, RabbitMQ Management e OTLP não ganharam exposição pública indevida.
- [ ] Preservei secrets externos ao repositório e Aspire/AppHost/Dashboard restritos ao desenvolvimento, com autenticação e acesso local; o deployment de produção continua definido pelo Compose/Nginx/Collector.

</details>

## Breaking changes e implantação

<!-- Declare “nenhuma” ou descreva contratos/configurações afetados, adaptação dos consumidores, ordem de implantação/migrations e recuperação/rollback. -->

## Documentação

- [ ] Atualizei README/docs e exemplos sem secrets para comportamento, configurações ou variáveis de ambiente alterados, ou justifiquei a não aplicabilidade abaixo.

Documentação alterada / justificativa:

## Evidências e pontos de atenção para o revisor

<!-- Opcional: resultados de testes, logs ou respostas HTTP sanitizados; screenshots não são obrigatórios. Destaque riscos, limitações e trechos que merecem revisão adicional. -->
