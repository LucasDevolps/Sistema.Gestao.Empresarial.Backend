# IIS + Cloudflare Tunnel: preparação para publicação pública

O backend e o frontend continuam nas portas locais 9081 e 9080. O perfil público
é opcional e exige duas origens HTTPS configuradas no script existente
`scripts/publish-local-iis.ps1`. O script não instala nem configura cloudflared,
não altera DNS, firewall ou roteador e não recebe credenciais do Tunnel.

## Diagnóstico e domínios

Antes desta mudança, o Angular carregava `public/config.json`, com `/api`, e o IIS
do frontend encaminhava `/api` e `/health` via ARR para o backend. A API não tinha
política CORS. A publicação bloqueava `config.json` no IIS, levando o Angular a
usar o valor padrão. A API confiava apenas no loopback IPv4, e o health público
incluía nomes e descrições dos checks. JWT e refresh token já ficavam somente em
memória no navegador, sem cookies de autenticação.

Os nomes solicitados **não são utilizáveis como hostnames públicos nesta
configuração**:

| Nome provisório | Resultado |
| --- | --- |
| `app.sistema-gerenciador-empresarial` | O sufixo `sistema-gerenciador-empresarial` não é um TLD delegado. |
| `app.sistema-gerenciador-empresarial-backend` | O sufixo `sistema-gerenciador-empresarial-backend` não é um TLD delegado. |

A consulta à [lista oficial de TLDs da IANA](https://data.iana.org/TLD/tlds-alpha-by-domain.txt)
foi realizada antes das alterações, em 08/09/2026. Os nomes têm aparência de DNS,
mas não pertencem a uma zona pública delegada. Nenhum `.com`, `.com.br` ou outro
domínio foi inventado. É necessário fornecer dois FQDNs reais sob zonas que você
controle e que sejam utilizáveis na sua conta Cloudflare.

## Configuração por ambiente

| Entrada de publicação | Uso |
| --- | --- |
| `SGE_PUBLIC_FRONTEND_ORIGIN` / `-PublicFrontendOrigin` | Origem HTTPS real do frontend, sem barra final, caminho ou porta alternativa. |
| `SGE_PUBLIC_BACKEND_ORIGIN` / `-PublicBackendOrigin` | Origem HTTPS real da API, sem `/api`, barra final ou porta alternativa. |

Forneça as duas ou nenhuma. As variáveis são configurações públicas, não secrets.
O script verifica sintaxe e recusa os nomes provisórios, mas não comprova registro,
propriedade de domínio, DNS, certificado ou configuração do Tunnel.

Depois de definir as duas variáveis com os FQDNs reais, execute no backend, em
PowerShell elevado, o script de publicação já existente:

```powershell
.\scripts\publish-local-iis.ps1 -FrontendPort 9080 -BackendPort 9081
```

Use as versões desta mudança nos dois repositórios. A publicação existente também
valida dependências e pode reaplicar o overlay de infraestrutura local; leia
`local-iis-deployment.md` antes de executá-la. Não é necessário mudar portas.

Sem as duas variáveis/parâmetros, o script publica o modo local original. Não
mantenha um Tunnel apontando para esse modo: só exponha o perfil público depois
de confirmar o checklist abaixo. Uma republicação não deve perder as variáveis
do perfil escolhido; mantenha-as no ambiente operacional de publicação.

O script gera somente no diretório publicado do frontend:

| Propriedade de `config.json` | Modo local | Modo público |
| --- | --- | --- |
| `apiBaseUrl` | `/api` | Origem de `SGE_PUBLIC_BACKEND_ORIGIN` seguida de `/api` |
| `localApiBaseUrl` | Ausente | `/api`, aplicada somente quando a página está em loopback |

`public/config.json` no código continua com `/api`. Não há environments paralelos
nem URLs públicas espalhadas pelos services. O `config.json` servido é público e
não pode conter segredos. IIS permite seu carregamento e desabilita cache desse
arquivo e de `index.html`; bundles de produção continuam sem source maps.

O script define no Application Pool da API:

| Propriedade | Perfil público |
| --- | --- |
| `DOTNET_ENVIRONMENT`, `ASPNETCORE_ENVIRONMENT` | `Production` |
| `AllowedHosts` | `localhost` e o host exato da API pública |
| `Cors__AllowedOrigins__0` | A origem exata de `SGE_PUBLIC_FRONTEND_ORIGIN` |
| `ReverseProxy__Enabled` | `true` |
| `ReverseProxy__UseCloudflareHeaders` | `true` |
| `ReverseProxy__ForwardLimit` | `1` |
| `ReverseProxy__KnownProxies__0`, `__1` | `127.0.0.1`, `::1` |
| `Swagger__Enabled` | `false` |

Os secrets existentes continuam na estratégia atual de variáveis do Application
Pool, fora de `web.config`, bundles e Git. Não adicione token do Tunnel ao `.env`
da aplicação, ao script ou ao diretório publicado.

## Limite de confiança do proxy

Este perfil suporta especificamente **cloudflared no mesmo Windows → IIS
in-process → ASP.NET Core**, com os dois sites restritos aos bindings de loopback.
O IIS in-process não acrescenta um salto de reverse proxy entre o conector e a
aplicação. Hosting out-of-process, ARR entre o Tunnel e a API, outro host, Worker
Cloudflare que altere a identidade do visitante ou outro proxy requerem nova
análise; não aumente `ForwardLimit` para tentar fazê-los funcionar.

O middleware padrão do ASP.NET Core aceita `CF-Connecting-IP` e
`X-Forwarded-Proto` somente de proxies conhecidos. No perfil público, ignora
`X-Forwarded-For`, `X-Real-IP` e `X-Forwarded-Host`, exige simetria entre IP e
esquema e processa um salto. O Host real deve ser preservado e é filtrado por
`AllowedHosts`. O perfil Nginx/local mantém o processamento anterior de XFF.

**Loopback não autentica um processo.** Administradores e processos locais capazes
de chamar o IIS fazem parte da fronteira de confiança. A aplicação não consegue
provar que uma conexão local veio de cloudflared. A segurança contra falsificação
pela Internet depende de o IIS não ter outra entrada e de Cloudflare sobrescrever
o header de identidade. CORS não substitui esse isolamento nem a autenticação.

Mantenha o Windows e as contas de serviço protegidos; não acrescente bindings
LAN/wildcard. Não habilite transformações que removam o IP do visitante, não use
Pseudo IPv4 em modo `Overwrite Headers` e não introduza Workers que reescrevam
`CF-Connecting-IP`. Não habilite `ASPNETCORE_FORWARDEDHEADERS_ENABLED` como atalho
para confiar em qualquer proxy. Não registre headers completos para diagnóstico.

Referências: [headers do Cloudflare](https://developers.cloudflare.com/fundamentals/reference/http-headers/)
e [proxy/IIS no ASP.NET Core](https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/proxy-load-balancer?view=aspnetcore-10.0).

## Segurança da aplicação

- CORS usa origens exatas, métodos e headers necessários ao contrato atual. Não
  habilita cookies/`AllowCredentials`, wildcard ou `AllowAnyOrigin`. Expõe somente
  `Retry-After` e `X-Correlation-ID` como headers adicionais de resposta. Sem
  configuração, nenhuma origem externa é autorizada. HTTP de loopback em CORS é
  permitido somente em `Development`; o IIS local usa mesma origem e não precisa dele.
- HTTPS redirection e HSTS permanecem ativos. No perfil público, a porta externa
  de redirecionamento é 443. A exceção HTTP exige simultaneamente Host `localhost`
  e IP de loopback, preservando o acesso local; não abrange o host público.
- JWT, issuer/audience, refresh rotativo, revogação, permissões e sessão permanecem
  inalterados. Não são URLs de callback nem precisam mudar com o domínio. Os
  tokens continuam somente em memória. O interceptor compara origem e limite de
  caminho da API, impedindo o envio do Bearer a URLs semelhantes ou a outros recursos.
- `/health/live` e `/health/ready` retornam somente `{"status":"Healthy"}` ou o
  estado correspondente, preservando os status HTTP do health middleware. Nomes,
  descrições, duração e exceções ficam fora dessas respostas públicas. `/health`
  continua autenticado; checks reais de SQL/Redis continuam executando.
- Swagger exige simultaneamente `Development` e `Swagger:Enabled=true`, mesmo se
  alguém habilitar a flag em Production. `/swagger` e `/openapi` não são expostos.
- Rate limiting continua usando a identidade/IP resolvidos antes da autenticação
  e da limitação; limites e regras de negócio não mudaram. Auditoria e OpenTelemetry
  permanecem internos, sem nova captura de corpos ou headers. A mudança não constitui
  uma garantia de redação universal de exceções em todos os logs existentes.
- O frontend recebe CSP com `connect-src` limitado à própria origem e à API
  configurada. Scripts só da própria origem, sem `unsafe-eval`; estilos inline
  permanecem permitidos para os estilos de componentes do Angular, como no perfil
  Docker existente. Há proteção contra framing, sniffing e vazamento de Referer.
- No perfil público, `/api` e `/health` pelo hostname do frontend retornam 404;
  os pedidos públicos vão ao hostname da API. O proxy ARR continua disponível pelo
  frontend local, sem criar uma segunda entrada pública para a API.

## Configuração manual no Cloudflare

No Tunnel existente, crie duas **Published application routes**. Preencha
Subdomain/Domain de acordo com os dois FQDNs reais escolhidos. Os valores abaixo
são referências às configurações, não nomes de domínio a copiar literalmente.

| Campo | Frontend | Backend |
| --- | --- | --- |
| Public hostname (sem `https://`) | Host de `SGE_PUBLIC_FRONTEND_ORIGIN` | Host de `SGE_PUBLIC_BACKEND_ORIGIN` |
| Path | Vazio | Vazio |
| Service / Type | HTTP | HTTP |
| Service / URL | `localhost:9080` | `localhost:9081` |
| HTTP Host Header | Host público exato do frontend, sem esquema ou porta | Host público exato da API, sem esquema ou porta |

Os destinos completos são **`http://localhost:9080` e `http://localhost:9081`**.
Não acrescente `/login`, `/api` ou `/health/ready` ao destino do Tunnel. As rotas
permanecem nas aplicações. Não use `localhost` como HTTP Host Header do Tunnel:
isso confundiria a entrada pública com o modo local e impediria HSTS na API.

Antes de disponibilizar:

1. Confirme zona DNS, posse dos domínios e certificados HTTPS válidos no edge.
2. Ative redirecionamento HTTP → HTTPS no edge (Always Use HTTPS ou regra
   equivalente) para ambos os hosts, antes de enviar tráfego à origem HTTP local.
3. Preserve Authorization, Content-Type, Origin, preflight OPTIONS e os headers
   de IP/esquema gerados pelo edge. Não proteja o preflight com uma página de login
   ou challenge incompatível com CORS. Se usar Cloudflare Access, configure-o
   explicitamente para esse fluxo; a aplicação não fornece token de serviço no SPA.
4. Não aplique Cache Everything na API. Configure bypass de cache no hostname da
   API e em `config.json`/HTML do frontend; respeite `Cache-Control`.
5. Confirme os bindings IIS `127.0.0.1:9080`, `[::1]:9080`, `127.0.0.1:9081` e
   `[::1]:9081`, com hosting da API **in-process**. Não abra firewall/roteador.
6. Publique a configuração dos dois repositórios e execute os checks abaixo.

Veja [rotas públicas do Tunnel](https://developers.cloudflare.com/cloudflare-one/networks/connectors/cloudflare-tunnel/routing-to-tunnel/).
Nenhum Tunnel Token é solicitado, gerado ou salvo por esta implementação.

## URLs e validação operacional

| Acesso | URL |
| --- | --- |
| Frontend local | `http://localhost:9080` |
| Login local | `http://localhost:9080/login` |
| Backend local | `http://localhost:9081` |
| Readiness local | `http://localhost:9081/health/ready` |
| Login público | Origem real de `SGE_PUBLIC_FRONTEND_ORIGIN` + `/login` |
| API pública | Origem real de `SGE_PUBLIC_BACKEND_ORIGIN` + `/api` |
| Readiness público | Origem real de `SGE_PUBLIC_BACKEND_ORIGIN` + `/health/ready` |

As URLs pretendidas `https://app.sistema-gerenciador-empresarial/login` e
`https://app.sistema-gerenciador-empresarial-backend/health/ready` permanecem apenas
provisórias; não é possível afirmar que funcionem publicamente com esses nomes.

Após configurar o ambiente real, confirme `/login`, `config.json` HTTP 200, login,
refresh, `/api/auth/me`, logout, 401/403, preflight permitido/negado, readiness,
HSTS e ausência de Swagger. Envie XFF/X-Real-IP/CF-Connecting-IP falsificados pela
entrada pública controlada e confirme no perímetro/auditoria autorizada que o IP
resultante é o do visitante, sem capturar tokens ou dados reais. Isso é uma
verificação de infraestrutura que os testes unitários não substituem.

## Validações executadas nesta preparação

Arquivos desta mudança (caminhos relativos a cada repositório):

| Repositório / arquivo | Alteração e motivo |
| --- | --- |
| Backend: `src/Sistema.Gestao.Empresarial.Api/Program.cs` | Ordena CORS no pipeline, limita Swagger a Development, preserva HTTPS local/público e seleciona health público mínimo. |
| Backend: `src/Sistema.Gestao.Empresarial.Api/appsettings.json` | Defaults sem origens CORS externas e perfil Cloudflare desabilitado. |
| Backend: `src/Sistema.Gestao.Empresarial.Api/Security/HttpSecurityOptions.cs` | Opção explícita para o perfil de headers Cloudflare. |
| Backend: `src/Sistema.Gestao.Empresarial.Api/Security/TrustedProxyConfiguration.cs` | Valida topologia, limita proxies e headers e preserva Host/HTTPS. |
| Backend: `src/Sistema.Gestao.Empresarial.Api/Security/FrontendCorsConfiguration.cs` | Política CORS restrita, configurável e validada no startup. |
| Backend: `src/Sistema.Gestao.Empresarial.Api/Health/HealthResponseWriter.cs` | Resposta pública sem nomes, descrições ou exceções de infraestrutura. |
| Backend: `scripts/publish-local-iis.ps1` | Gera configurações públicas/locais, libera config.json, ajusta CSP/cache, preserva portas e interrompe publicação se testes frontend falharem. |
| Backend: `scripts/tests/publish-iis-configuration.tests.ps1` | Valida a configuração real gerada sem executar publicação. |
| Backend: `tests/Sistema.Gestao.Empresarial.IntegrationTests/Security/PublicIisSecurityTests.cs` | Regressões de CORS, proxy/IP, HTTPS, Swagger e health. |
| Backend: `docs/local-iis-deployment.md` | Distingue os perfis local e público. |
| Backend: `docs/iis-cloudflare-deployment.md` | Este diagnóstico, configuração, limites de confiança, validações e pendências. |
| Frontend: `src/app/core/config/app-config.ts` | Valida URL runtime, suporta override local explícito e compara destino da API com segurança. |
| Frontend: `src/app/core/auth/auth.interceptor.ts` | Restringe Bearer à origem e ao caminho exatos da API. |
| Frontend: `src/app/core/config/app-config.spec.ts` | Testa destinos públicos/locais e rejeição de URLs inseguras. |
| Frontend: `docs/IIS-CLOUDFLARE.md` | Orienta configuração runtime e preservação do desenvolvimento. |
| Frontend: `README.md` | Torna o novo guia acessível. |

```powershell
dotnet build Sistema.Gestao.Empresarial.sln --configuration Release --no-restore
dotnet test Sistema.Gestao.Empresarial.sln --configuration Release --no-restore --filter 'Category!=RealInfrastructure'
powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts/tests/publish-iis-configuration.tests.ps1
```

No frontend: `npm run build:prod`, `npm run test:ci`, `npm run lint`.

Build .NET sem erros/avisos; 7 testes unitários e 96 testes de integração passaram.
O frontend compilou para produção e os 57 testes passaram no ChromeHeadless.
O teste PowerShell 5.1 validou a configuração local/pública gerada em diretório
temporário, sem executar publicação, ler `.env`, instalar software ou alterar IIS.
`-ExecutionPolicy Bypass` vale apenas para esse processo de teste, sem mudar a
política do Windows. Testes `RealInfrastructure` não foram executados: a mudança
não altera persistência/mensageria e não foi feita operação no banco.

O IIS já publicado respondeu HTTP 200 em `/login` e `/health/ready`; seu
`config.json` respondeu 404, corrigido no script para a próxima publicação. Esses
smoke checks descrevem a instalação anterior, não uma republicação desta mudança.
A validação pública ponta a ponta depende dos FQDNs reais, da republicação e do
Tunnel externo; não foi realizada nem simulada.
