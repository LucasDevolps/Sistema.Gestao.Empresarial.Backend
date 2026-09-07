# Política de Segurança

A segurança do **Sistema de Gestão Empresarial Hospitalar** é tratada como parte essencial do projeto.

Este documento descreve quais versões recebem suporte de segurança, como reportar possíveis vulnerabilidades e quais práticas devem ser seguidas durante testes e divulgação de falhas.

> [!IMPORTANT]
> Este repositório é público. **Issues abertas neste repositório também são públicas.**
> Nunca publique credenciais, tokens, dados pessoais, dados hospitalares reais, chaves privadas ou detalhes de exploração que possam permitir o comprometimento imediato do sistema.

---

## Versões suportadas

O projeto ainda não possui releases formais publicadas.

Neste momento, o suporte de segurança é direcionado à versão corrente mantida na branch principal:

| Versão / Branch | Suporte de segurança |
| --- | --- |
| `main` | ✅ Suportada |
| branches antigas ou não mantidas | ❌ Não suportadas |

Quando releases estáveis passarem a ser publicadas, esta seção deverá ser atualizada para indicar explicitamente quais versões ainda recebem correções de segurança.

---

## Como reportar uma vulnerabilidade

Se você identificar uma possível vulnerabilidade, abra uma **GitHub Issue** neste repositório.

Utilize um título que permita identificar rapidamente o tipo do relato, por exemplo:

```text
[SECURITY] Possível falha de autorização em endpoint autenticado
```

O relato inicial deve conter somente informações que possam ser publicadas com segurança.

Inclua, quando aplicável:

- resumo objetivo da vulnerabilidade;
- componente afetado;
- endpoint ou funcionalidade afetada;
- branch, commit ou versão afetada;
- impacto estimado;
- severidade sugerida;
- comportamento esperado;
- comportamento observado;
- passos **sanitizados** para reprodução;
- ambiente utilizado durante o teste;
- evidências ou logs completamente redigidos;
- possível mitigação, caso conhecida.

### Não publique na Issue

Não inclua:

- senhas;
- access tokens;
- refresh tokens;
- API keys;
- cookies de autenticação;
- `Authorization` headers;
- connection strings;
- chaves JWT;
- private keys;
- certificados privados;
- arquivos `.env` reais;
- credenciais do SQL Server;
- credenciais do Redis;
- credenciais do RabbitMQ;
- dumps de banco de dados;
- CPF ou outros documentos pessoais;
- informações pessoais identificáveis (PII);
- informações médicas ou hospitalares reais;
- dados de pacientes;
- dados de funcionários reais;
- segredos de CI/CD;
- payloads destrutivos;
- exploits completos capazes de comprometer imediatamente uma instalação;
- qualquer informação que permita acesso não autorizado a um ambiente real.

Se a reprodução exigir informações que não podem ser publicadas com segurança, abra inicialmente a Issue com uma descrição sanitizada.

Os detalhes adicionais deverão ser compartilhados somente por um meio adequado combinado posteriormente com o mantenedor.

---

## Escopo de segurança

Relatos relacionados aos seguintes componentes são considerados relevantes:

### API ASP.NET Core

Incluindo, entre outros:

- autenticação;
- autorização;
- JWT;
- refresh tokens;
- sessões;
- logout e revogação;
- rate limiting;
- validação de entrada;
- exposição indevida de endpoints;
- vazamento de informações;
- tratamento de exceções;
- CORS;
- HTTPS;
- forwarded headers;
- spoofing de IP;
- IDOR;
- SSRF;
- path traversal;
- SQL Injection;
- command injection;
- deserialização insegura.

### Autorização e isolamento organizacional

Falhas que possam permitir:

- privilege escalation;
- bypass de permissões;
- autoelevação de privilégios;
- acesso cross-tenant;
- acesso indevido entre hospitais;
- leitura ou alteração de dados de outra organização;
- exposição de identificadores ou entidades internas indevidas.

### Autenticação e sessões

Falhas relacionadas a:

- geração ou validação de JWT;
- assinatura de tokens;
- expiração;
- refresh token;
- reutilização de refresh token;
- revogação;
- sessão única;
- expiração por inatividade;
- invalidação de sessão;
- brute force;
- enumeração de usuários;
- bypass de login.

### Banco de dados / SQL Server

Incluindo:

- SQL Injection;
- privilégios excessivos;
- credenciais expostas;
- portas expostas indevidamente;
- acesso não autorizado;
- migrations inseguras;
- vazamento de dados;
- falhas de segregação organizacional.

### Redis

Incluindo:

- ausência ou bypass de autenticação;
- ACL incorreta;
- exposição pública;
- vazamento de sessão;
- persistência indevida de informações sensíveis;
- TTL incorreto;
- inconsistências de invalidação;
- comportamento inseguro em caso de indisponibilidade.

### RabbitMQ / MassTransit

Incluindo:

- credenciais expostas;
- Management UI acessível indevidamente;
- virtual host inseguro;
- mensagens não confiáveis;
- serialização insegura;
- replay;
- problemas de idempotência;
- Inbox / Outbox;
- retries inseguros;
- poison messages;
- vazamento de dados em mensagens.

### Nginx e perímetro HTTP

Incluindo:

- configuração TLS;
- headers de segurança;
- proxy headers;
- `X-Forwarded-For`;
- identificação incorreta do IP real;
- bypass de rate limit;
- connection limits;
- request limits;
- timeouts;
- endpoints internos expostos;
- configuração incorreta do reverse proxy.

### Containers e infraestrutura

Incluindo:

- Dockerfiles;
- Docker Compose;
- imagens base;
- execução como `root`;
- capabilities excessivas;
- portas publicadas;
- redes;
- volumes;
- secrets;
- variáveis de ambiente;
- configurações específicas de produção.

### CI/CD e supply chain

Incluindo:

- GitHub Actions;
- CodeQL;
- Trivy;
- SBOM;
- dependências NuGet;
- actions de terceiros;
- permissões excessivas do `GITHUB_TOKEN`;
- workflow injection;
- dependency confusion;
- comprometimento de dependências;
- exposição de secrets em pipelines.

### Logs e observabilidade

Incluindo possíveis vazamentos por:

- logs estruturados;
- auditoria HTTP;
- OpenTelemetry;
- traces;
- métricas;
- Collector;
- artifacts do CI.

---

## Classificação de severidade

Os relatos poderão ser classificados de acordo com o impacto técnico e operacional.

### Critical

Exemplos:

- execução remota de código sem autenticação;
- bypass completo de autenticação;
- comprometimento de credenciais administrativas;
- acesso cross-tenant amplo a dados sensíveis;
- comprometimento completo da infraestrutura;
- exposição massiva de dados sensíveis.

### High

Exemplos:

- privilege escalation relevante;
- bypass de autorização;
- acesso indevido a informações de outra organização;
- comprometimento de sessões;
- SQL Injection explorável;
- acesso administrativo não autorizado.

### Medium

Exemplos:

- exposição limitada de informações;
- controles de segurança parcialmente contornáveis;
- enumeração relevante;
- configuração insegura que exija condições específicas para exploração.

### Low

Exemplos:

- hardening incompleto;
- informações técnicas de baixo impacto;
- configurações que aumentam risco, mas não representam comprometimento direto.

### Informational

Observações de segurança e recomendações que não representam uma vulnerabilidade explorável por si só.

A classificação final poderá ser ajustada após análise técnica.

---

## Processo de triagem

O objetivo do mantenedor é:

1. confirmar o recebimento do relato;
2. validar se o comportamento representa uma vulnerabilidade real;
3. avaliar impacto e severidade;
4. identificar versões ou componentes afetados;
5. preparar a correção;
6. validar a correção com testes;
7. disponibilizar a correção de forma controlada;
8. divulgar detalhes adicionais somente quando isso não aumentar o risco para instalações vulneráveis.

Sempre que possível:

- o recebimento deverá ser reconhecido em até **5 dias úteis**;
- a triagem inicial deverá ocorrer em até **10 dias úteis**.

Esses períodos são metas de resposta e podem variar conforme complexidade, disponibilidade do mantenedor e severidade do problema.

Vulnerabilidades críticas terão prioridade máxima.

---

## Divulgação responsável

Solicitamos que pesquisadores evitem divulgar publicamente detalhes que permitam exploração prática antes que:

- o problema tenha sido confirmado;
- uma correção esteja disponível;
- instalações afetadas tenham tido oportunidade razoável de atualização.

Após a correção, detalhes técnicos adicionais poderão ser discutidos publicamente quando isso for seguro.

O objetivo desta política não é impedir pesquisa legítima, mas reduzir o risco de que um relato de segurança seja usado para atacar ambientes reais.

---

## Regras para testes de segurança

Testes devem ser realizados somente:

- em ambientes controlados;
- em instâncias pertencentes ao próprio pesquisador;
- em ambientes para os quais exista autorização explícita.

Não é autorizado, em nome deste projeto:

- acessar dados de terceiros;
- acessar ambientes de produção sem autorização;
- acessar contas de outros usuários;
- realizar engenharia social;
- realizar phishing;
- tentar obter credenciais de usuários;
- realizar ataques físicos;
- executar malware;
- causar indisponibilidade proposital;
- executar ataques volumétricos de DoS ou DDoS;
- destruir ou alterar dados reais;
- persistir acesso após comprovar uma vulnerabilidade;
- realizar movimentação lateral;
- extrair dados além do mínimo necessário para demonstrar o problema.

Ao comprovar uma vulnerabilidade, utilize a menor quantidade possível de dados e ações necessárias.

---

## Proteção de dados

Este projeto foi desenvolvido para um contexto de gestão empresarial/hospitalar.

Por isso, qualquer teste deve considerar que instalações reais podem manipular informações sensíveis.

Nunca utilize dados reais de pacientes, funcionários ou organizações para criar provas de conceito.

Prefira sempre:

- dados fictícios;
- usuários de teste;
- organizações de teste;
- tokens temporários;
- ambientes locais;
- containers descartáveis.

Evidências anexadas a Issues devem ser sanitizadas.

---

## Credenciais e secrets encontrados

Caso encontre uma credencial ou secret aparentemente válido no código, histórico Git, imagem de container ou artifact:

1. não tente utilizá-lo contra um ambiente real;
2. não publique o valor;
3. não copie o segredo para a Issue;
4. informe apenas o arquivo ou contexto aproximado;
5. trate o valor como potencialmente comprometido.

O mantenedor deverá considerar a rotação ou revogação da credencial afetada.

---

## Dependências de terceiros

Vulnerabilidades originadas exclusivamente em dependências externas também podem ser reportadas quando afetarem este projeto.

Sempre que possível, informe:

- pacote ou imagem afetada;
- versão utilizada;
- CVE/GHSA, caso exista;
- versão corrigida conhecida;
- impacto específico neste projeto.

A mera existência de um CVE em uma dependência não significa necessariamente que este sistema seja explorável.

O impacto será avaliado considerando como o componente é utilizado.

---

## Itens normalmente fora de escopo

Salvo quando houver impacto específico neste projeto, normalmente não são considerados vulnerabilidades próprias do sistema:

- problemas exclusivamente no GitHub.com;
- falhas em serviços externos não controlados pelo projeto;
- vulnerabilidades já corrigidas na `main`;
- alertas automáticos sem demonstração de impacto;
- ausência de headers sem consequência de segurança prática;
- ataques que dependam exclusivamente de acesso administrativo legítimo;
- ataques de engenharia social;
- ataques físicos;
- ataques volumétricos de DoS/DDoS.

Problemas de dependências ou infraestrutura podem continuar sendo reportados quando houver risco concreto para instalações deste projeto.

---

## Boas práticas ao abrir o relato

Um bom relato permite reproduzir e corrigir o problema sem expor informações perigosas.

Exemplo de estrutura:

```text
Título:
[SECURITY] Bypass de autorização em <componente>

Resumo:
Descrição curta e sanitizada.

Componente afetado:
API / Worker / Nginx / Redis / RabbitMQ / SQL Server / CI etc.

Commit ou versão:
<hash ou branch>

Severidade sugerida:
Low / Medium / High / Critical

Impacto:
Descrição do impacto possível.

Passos de reprodução:
Passos mínimos utilizando dados fictícios.

Resultado observado:
...

Resultado esperado:
...

Ambiente:
Local / Docker / sistema operacional / versão do .NET.

Evidências:
Somente logs ou imagens sanitizadas.
```

---

## Segurança acima da conveniência

Relatos de segurança serão avaliados priorizando:

1. proteção de dados;
2. isolamento entre organizações;
3. autenticação;
4. autorização;
5. proteção de sessões;
6. mínimo privilégio;
7. defesa em profundidade;
8. segurança da cadeia de dependências;
9. disponibilidade;
10. auditabilidade.

Obrigado por ajudar a tornar o **Sistema de Gestão Empresarial Hospitalar** mais seguro.
