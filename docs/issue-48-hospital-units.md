# Issue #48 — cadastro completo de unidades hospitalares

Implementação da [issue #48](https://github.com/LucasDevolps/Sistema.Gestao.Empresarial.Backend/issues/48).

## Diagnóstico e arquitetura

Antes desta entrega, `UnidadeHospitalar` possuía organização, nome e os campos herdados de identidade/auditoria/status. O catálogo oferecia somente listagem e consulta por Guid. Os GETs exigiam `FUNCIONARIO_VISUALIZAR`, e havia unicidade por organização + nome.

A mesma entidade, tabela, `DbSet` e relacionamentos foram evoluídos. `Nome` continua representando o nome fantasia e é exposto como `name`; o construtor anterior e `HospitalUnitReferenceResponse` permanecem compatíveis com bootstrap, funcionários e setores. O cadastro completo usa `CadastroUnidadeHospitalar`, um record de dados normalizados, sem identidade própria nem tabela adicional. As propriedades são colunas da unidade existente. Os enums de tipo/natureza são serializados como strings.

`OrganizationCatalogService` permanece o serviço do módulo. O arquivo parcial `OrganizationCatalogService.HospitalUnits.cs` concentra os casos de uso e reutiliza os helpers de transação, `TimeProvider`, Audit + Outbox e tradução de erros SQL. Não há outro módulo hospitalar. As integrações são clientes tipados independentes; nenhuma chamada externa ocorre em criação, atualização, consulta de unidade ou listagem.

A organização é resolvida a partir do usuário ativo e seu funcionário, seguindo o modelo atual. `organizationGuid` no cadastro é opcional e apenas confirma a organização resolvida; não permite criar em outra organização nem transferir uma unidade. Leituras/edições/status por Guid fora do escopo retornam 404. O filtro de organização nunca amplia o escopo.

## Endpoints e permissões

Prefixo: `/api/unidades-hospitalares`. Todos exigem sessão autenticada e permissão específica.

| Método | Sufixo | Finalidade / sucesso | Permissão |
|---|---|---|---|
| GET | vazio | Listagem paginada resumida / 200 | `UNIDADE_HOSPITALAR_VISUALIZAR` |
| GET | `/{unitGuid}` | Cadastro completo / 200 | `UNIDADE_HOSPITALAR_VISUALIZAR` |
| POST | vazio | Criação / 201 + Location | `UNIDADE_HOSPITALAR_CRIAR` |
| PUT | `/{unitGuid}` | Substituição do cadastro / 200 | `UNIDADE_HOSPITALAR_EDITAR` |
| PATCH | `/{unitGuid}/status` | `{ "active": true/false }` / 200 | `UNIDADE_HOSPITALAR_EDITAR` |
| GET | `/consulta-cnpj?cnpj=...` | Sugestões BrasilAPI / 200 | `UNIDADE_HOSPITALAR_VISUALIZAR` |
| GET | `/consulta-cep?cep=...` | Sugestões ViaCEP / 200 | `UNIDADE_HOSPITALAR_VISUALIZAR` |
| POST | `/possiveis-duplicidades` | Alerta de similaridade / 200 | `UNIDADE_HOSPITALAR_VISUALIZAR` |

Filtros de listagem: `search` (nome fantasia), `legalName`, `cnpj`, `cnes`, `city`, `state`, `organizationGuid`, `active`, `page` (padrão 1), `pageSize` (padrão 50, máximo 100). Ordenação por nome e Guid. Filtragem, paginação e projeção ocorrem no SQL com `AsNoTracking`; listagem não seleciona contatos, responsáveis ou informações regulatórias.

Erros seguem ProblemDetails: 400 para request inválida; 401/403 para autenticação/permissão/escopo; 404 para unidade inexistente/fora do escopo ou identificador não encontrado no provedor; 409 para duplicidade/conflito de concorrência; 422 para invariantes de domínio; 503 para consulta auxiliar indisponível. Conflitos de chave usam `code=DUPLICATE_BUSINESS_KEY` e `field=cnpj|cnes|internalCode`, conforme mecanismo existente. SQL e stack trace não são retornados.

As permissões novas são semeadas e concedidas idempotentemente ao `ADMINISTRADOR_INICIAL` existente. Instalações novas recebem o catálogo pelo bootstrap. Outros perfis devem receber as novas permissões pelo mecanismo administrativo existente; `FUNCIONARIO_VISUALIZAR` deixa de autorizar os endpoints hospitalares.

Exemplos executáveis: [hospital-units.http](../hospital-units.http). Contratos completos também disponíveis no OpenAPI existente.

## Cadastro e validações

`HospitalUnitRegistrationRequest` é usado tanto em POST quanto em PUT. PUT substitui todos os dados cadastrais; campos opcionais omitidos tornam-se nulos/default. O estado ativo é alterado somente no endpoint de status. Uma criação nasce ativa.

Obrigatórios: `name`, `postalCode`, `street`, `number`, `district`, `city`, `state`. Para unidade com CNPJ próprio, informar `hasOwnCnpj=true`: exige CNPJ. Qualquer CNPJ informado exige razão social. Não se presume que toda unidade possua CNPJ próprio.

CNPJ: somente dígitos ASCII após remoção da máscara, 14 dígitos, rejeição de sequências repetidas e cálculo dos dois verificadores. CEP: 8 dígitos ASCII após remover hífen. Ambos são persistidos sem máscara. CNES opcional: 7 dígitos. Código interno e sigla são aparados e convertidos para maiúsculas; código interno usa collation explícita insensível a caixa. E-mails, telefones, URL HTTP/HTTPS, UF, DDD e IBGE são validados quando informados. Strings possuem limites alinhados às colunas EF. Leitos não podem ser negativos; leitos de UTI não superam total quando ambos são conhecidos.

Tipos: `HospitalGeral`, `HospitalEspecializado`, `HospitalDia`, `UnidadeProntoAtendimento`, `Clinica`, `Maternidade`, `Ambulatorio`, `CentroDiagnostico`, `Outro`. Naturezas: `Publica`, `Privada`, `Filantropica`, `Conveniada`, `Outra`.

Datas usam `DateOnly` (`yyyy-MM-dd`), de acordo com as convenções do domínio. Flags operacionais são nullable, permitindo distinguir desconhecido de falso. Responsáveis são dados institucionais: nenhum usuário é criado automaticamente. `secondaryCnaes` no cadastro é texto opcional de até 2000 caracteres; na consulta BrasilAPI é uma lista estruturada de código/descrição, que o frontend pode apresentar e consolidar antes de salvar.

| Campo JSON | Campo do domínio | Tipo / limite |
|---|---|---|
| `name` | `Nome` | `string`, 200 caracteres |
| `legalName` | `RazaoSocial` | `string?`, 200 caracteres |
| `cnpj` | `Cnpj` | `string?`, 14 caracteres |
| `hasOwnCnpj` | `PossuiCnpjProprio` | `bool` |
| `cnes` | `Cnes` | `string?`, 7 caracteres |
| `unitType` | `Tipo` | `TipoUnidadeHospitalar?` |
| `nature` | `Natureza` | `NaturezaUnidadeHospitalar?` |
| `internalCode` | `CodigoInterno` | `string?`, 50 caracteres |
| `acronym` | `Sigla` | `string?`, 20 caracteres |
| `activityStartDate` | `InicioAtividades` | `DateOnly?` |
| `registrationStatus` | `SituacaoCadastral` | `string?`, 100 caracteres |
| `openingDate` | `DataAbertura` | `DateOnly?` |
| `legalNature` | `NaturezaJuridica` | `string?`, 200 caracteres |
| `primaryCnae` | `CnaePrincipal` | `string?`, 200 caracteres |
| `secondaryCnaes` | `CnaesSecundarios` | `string?`, 2000 caracteres |
| `stateRegistration` | `InscricaoEstadual` | `string?`, 50 caracteres |
| `municipalRegistration` | `InscricaoMunicipal` | `string?`, 50 caracteres |
| `postalCode` | `Cep` | `string?`, 8 caracteres |
| `street` | `Logradouro` | `string?`, 200 caracteres |
| `number` | `Numero` | `string?`, 30 caracteres |
| `complement` | `Complemento` | `string?`, 150 caracteres |
| `district` | `Bairro` | `string?`, 100 caracteres |
| `city` | `Cidade` | `string?`, 100 caracteres |
| `state` | `Uf` | `string?`, 2 caracteres |
| `ibgeCode` | `CodigoIbge` | `string?`, 7 caracteres |
| `region` | `Regiao` | `string?`, 30 caracteres |
| `areaCode` | `Ddd` | `string?`, 2 caracteres |
| `addressReference` | `ReferenciaEndereco` | `string?`, 300 caracteres |
| `phone` | `TelefonePrincipal` | `string?`, 30 caracteres |
| `secondaryPhone` | `TelefoneSecundario` | `string?`, 30 caracteres |
| `whatsapp` | `Whatsapp` | `string?`, 30 caracteres |
| `email` | `EmailInstitucional` | `string?`, 254 caracteres |
| `administrativeEmail` | `EmailAdministrativo` | `string?`, 254 caracteres |
| `website` | `Site` | `string?`, 500 caracteres |
| `extension` | `Ramal` | `string?`, 30 caracteres |
| `administrativeResponsibleName` | `ResponsavelAdministrativoNome` | `string?`, 200 caracteres |
| `administrativeResponsibleRole` | `ResponsavelAdministrativoCargo` | `string?`, 150 caracteres |
| `administrativeResponsibleEmail` | `ResponsavelAdministrativoEmail` | `string?`, 254 caracteres |
| `administrativeResponsiblePhone` | `ResponsavelAdministrativoTelefone` | `string?`, 30 caracteres |
| `technicalResponsibleName` | `ResponsavelTecnicoNome` | `string?`, 200 caracteres |
| `technicalResponsibleProfession` | `ResponsavelTecnicoProfissao` | `string?`, 150 caracteres |
| `technicalResponsibleCouncil` | `ResponsavelTecnicoConselho` | `string?`, 50 caracteres |
| `technicalResponsibleCouncilNumber` | `ResponsavelTecnicoRegistro` | `string?`, 50 caracteres |
| `technicalResponsibleCouncilState` | `ResponsavelTecnicoUf` | `string?`, 2 caracteres |
| `technicalResponsibleEmail` | `ResponsavelTecnicoEmail` | `string?`, 254 caracteres |
| `technicalResponsiblePhone` | `ResponsavelTecnicoTelefone` | `string?`, 30 caracteres |
| `clinicalDirectorName` | `DiretorClinicoNome` | `string?`, 200 caracteres |
| `clinicalDirectorCrm` | `DiretorClinicoCrm` | `string?`, 50 caracteres |
| `clinicalDirectorCrmState` | `DiretorClinicoUf` | `string?`, 2 caracteres |
| `clinicalDirectorEmail` | `DiretorClinicoEmail` | `string?`, 254 caracteres |
| `clinicalDirectorPhone` | `DiretorClinicoTelefone` | `string?`, 30 caracteres |
| `sanitaryPermit` | `AlvaraSanitario` | `string?`, 100 caracteres |
| `sanitaryPermitExpiry` | `ValidadeAlvaraSanitario` | `DateOnly?` |
| `operatingLicense` | `LicencaFuncionamento` | `string?`, 100 caracteres |
| `operatingLicenseExpiry` | `ValidadeLicencaFuncionamento` | `DateOnly?` |
| `regulatoryNotes` | `ObservacoesRegulatorias` | `string?`, 2000 caracteres |
| `open24Hours` | `Atendimento24h` | `bool?` |
| `hasEmergencyRoom` | `ProntoSocorro` | `bool?` |
| `hasInpatientCare` | `Internacao` | `bool?` |
| `hasIcu` | `Uti` | `bool?` |
| `totalBeds` | `TotalLeitos` | `int?` |
| `icuBeds` | `LeitosUti` | `int?` |
| `hasSurgicalCenter` | `CentroCirurgico` | `bool?` |
| `hasMaternity` | `Maternidade` | `bool?` |
| `hasOutpatientCare` | `AtendimentoAmbulatorial` | `bool?` |
| `notes` | `ObservacoesGerais` | `string?`, 2000 caracteres |

## Integrações auxiliares

Abstrações `ICnpjLookupService` e `ICepLookupService` na Application, implementadas na Infrastructure. `IHttpClientFactory`, URLs em Options, timeout padrão de 5 segundos (configurável entre 1 e 30), CancellationToken, limite de resposta de 512 KiB e nenhuma repetição automática. Logs registram somente status/tipo da falha; logging padrão de URLs desses clientes é removido.

Configuração em `HospitalLookups` (também via `HospitalLookups__BrasilApiBaseUrl`, `HospitalLookups__ViaCepBaseUrl`, `HospitalLookups__TimeoutSeconds`). Os padrões são `https://brasilapi.com.br/` e `https://viacep.com.br/`.

BrasilAPI: consulta explícita `/api/cnpj/v1/{cnpj}` após validar os verificadores. Mapeia razão social, nome fantasia, situação, abertura, natureza jurídica, CNAEs, telefone, e-mail e endereço. `inactiveRegistrationWarning=true` sinaliza situação não ativa; não bloqueia cadastro. O resultado não é persistido automaticamente.

ViaCEP: consulta explícita `/ws/{cep}/json/` após validar a estrutura. Retorna endereço, IBGE, DDD e região quando disponível. Não retorna número. `erro: true` (boolean/string) produz 404 com “CEP não encontrado”. CEPs de municípios que não retornam logradouro/bairro continuam podendo ser preenchidos manualmente.

Timeout, falha de rede/DNS, HTTP 5xx/429, JSON inválido ou payload incompatível retornam 503 com orientação de preenchimento manual. Cancelamento do chamador é propagado. A gravação não depende do resultado ou disponibilidade dessas consultas.

Referências de contrato: [BrasilAPI](https://brasilapi.com.br/docs#tag/CNPJ), [ViaCEP](https://viacep.com.br/).

## Similaridade

POST `/possiveis-duplicidades` recebe `name`, `legalName` opcional, `postalCode`, `number` e `excludeGuid` opcional. Restringe candidatos à mesma organização, CEP e número, ordena por Guid, examina até 100 candidatos e retorna até 20 alertas. Compara conjuntos de palavras do nome fantasia OU razão social após remover acentos/pontuação e normalizar caixa, com interseção/união >= 80%. É uma heurística limitada de auxílio ao operador, não garantia de detecção de todas as duplicidades. Nunca impede a gravação. Nomes iguais são permitidos se as chaves únicas não conflitarem.

## Banco e compatibilidade

Migration: `20260929110220_CompleteHospitalUnitRegistration` (+ Designer e snapshot). Adiciona colunas nullable; `PossuiCnpjProprio` recebe default false. Preserva `Nome`, IDs, Guids, dados e todas as FKs. Não recria nem apaga unidades. Endereço obrigatório é validado nos novos POST/PUT; unidades legadas podem permanecer com endereço nulo até serem completadas.

Índices únicos: `IX_UnidadesHospitalares_Cnpj` (`Cnpj IS NOT NULL`), `IX_UnidadesHospitalares_Cnes` (`Cnes IS NOT NULL`), `IX_UnidadesHospitalares_OrganizacaoId_CodigoInterno` (`CodigoInterno IS NOT NULL`). CNPJ e CNES são globais; código interno pertence ao escopo organizacional. As chaves permanecem reservadas em unidades inativas, inclusive em registros logicamente excluídos por outros mecanismos. Pré-checagem amigável e índices protegem, respectivamente, experiência e concorrência.

O índice organização + nome passa a ser não único para permitir alertas de similaridade sem bloqueio. Há índice organização + cidade + UF para pesquisa. Relacionamentos continuam Restrict; status não percorre nem modifica vínculos. Down remove os campos adicionados, como reversão de schema, e é bloqueado antes de alterar dados se nomes repetidos impossibilitarem restaurar a unicidade anterior. Não se deve usar rollback como estratégia de preservação dos novos dados cadastrais; manter backup e preferir correção por nova migration.

## Auditoria

Criação, atualização, inativação e reativação efetivas usam a transação existente com Audit + Outbox. Registram ator, instante UTC via TimeProvider, ação, Guid, correlation ID, trace ID, IP e snapshot institucional mínimo (Guid, nome, estado, código, tipo/natureza e cidade/UF). Atualização/status incluem before/after; contatos/dados pessoais dos responsáveis e payload externo são omitidos. Repetir o mesmo status é idempotente e não gera uma mutação fictícia.

Eventos: `UnidadeHospitalarCriada`, `UnidadeHospitalarAtualizada`, `UnidadeHospitalarInativada`, `UnidadeHospitalarReativada`, versão 1. A falha na Outbox reverte a unidade e a auditoria na mesma transação.

## Verificação

- `dotnet restore Sistema.Gestao.Empresarial.sln`: aprovado.
- `dotnet build Sistema.Gestao.Empresarial.sln --no-restore`: aprovado, zero warnings/erros.
- `dotnet test Sistema.Gestao.Empresarial.sln --no-build --no-restore`: 76 unitários + 203 integrações/API aprovados, zero falhas; 38 cenários de infraestrutura real separados por configuração. Esses 38 também passaram na execução WSL abaixo. Total combinado: 317 testes aprovados, sem cenários pendentes. Foram adicionados 80 casos (26 de domínio, 22 API CRUD/OpenAPI, 18 API de consultas, 10 de serviços e 4 SQL real).
- `wsl -d Ubuntu -- bash scripts/run-real-integration-tests-wsl.sh`: 38 aprovados, zero falhas/skips, incluindo os quatro novos testes SQL.
- `dotnet ef migrations has-pending-model-changes`: sem diferenças pendentes.
- Script SQL incremental gerado e revisado; migration aplicada em bancos descartáveis nos testes, sem migração do banco de desenvolvimento/produção.
- Testes HTTP usam a API real (controllers, políticas e middleware) com autenticação controlada e EF InMemory; testes SQL cobrem schema, migração legada, unicidade sob corrida, projeções, FKs e atomicidade. Provedores públicos usam somente HTTP fake.

## Critérios de aceite

| Critério | Status | Evidência |
|---|---|---|
| Cadastro completo e identificação | ✅ | `AllRegistrationSections_RoundTripThroughCreateAndUpdate_ListStaysSmall` |
| Validação CNPJ | ✅ | `UnidadeHospitalarTests.Cnpj_ChecksDigitsAndMasks` |
| CNPJ único global | ✅ | `GlobalKeysCrossOrganizations_InternalCodeCanRepeat`, `UniqueIndexes_ResolveRacesAndRollBackAuditAndOutbox` |
| BrasilAPI e dados para autopreenchimento | ✅ backend | `HospitalLookupApiTests.Success_MapsProviderFields` |
| Falha BrasilAPI não bloqueia cadastro | ✅ | `ProviderFailure_IsControlledAndManualCreateStillWorks` |
| Situação cadastral inativa apenas alerta | ✅ | `HospitalLookupServiceTests.InactiveRegistration_IsAnAlert_AndManualCreateRemainsAllowed` |
| ViaCEP e dados de endereço | ✅ backend | `HospitalLookupApiTests.Success_MapsProviderFields` |
| Número manual e campos editáveis | ✅ backend / DEPENDENTE DO FRONTEND na interface | Consulta CEP sem número; PUT aceita valores manuais |
| Falha ViaCEP não bloqueia cadastro | ✅ | `ProviderFailure_IsControlledAndManualCreateStillWorks` |
| CNES único | ✅ | Testes HTTP globais e corrida SQL por `cnes` |
| Código interno único por organização | ✅ | Teste HTTP entre organizações e corrida SQL por `internalCode` |
| Contatos | ✅ | Request validator, domínio e teste de round-trip completo |
| Responsáveis administrativo/técnico/diretor | ✅ | Teste de round-trip; sem criação de usuários |
| Regulatório | ✅ | Teste de round-trip completo |
| Operacional e leitos | ✅ | `InvalidBedCounts_AreRejected`, `InvalidBeds_Return400OnCreateAndUpdate` |
| Organização e isolamento | ✅ | `OtherOrganizationAndMissingGuid_AreInvisibleToReadsAndWrites`, teste HTTP correspondente |
| Listagem, pesquisa, filtros e paginação | ✅ | `FiltersAndPagination_AreAppliedBeforeSummaryProjection`, `SqlCrud_ProjectsSummaryAndPreservesRelationshipsOnDeactivation` |
| Edição e unicidade ignorando próprio registro | ✅ | `UpdateKeepsOwnCnpj_RejectsCnpjOfAnotherUnit` |
| Inativação/reativação | ✅ | `Crud_PreservesCompleteDataAndAuditsEachMutation` |
| Preservação de relacionamentos/histórico | ✅ | `StatusPreservesSectorsEmployeesAndHistoricalRelationships`, teste SQL de CRUD |
| Similaridade não bloqueante | ✅ | `SimilarityAlerts_DoNotBlockNamesOrExposeOtherOrganizations` |
| Auditoria e Outbox | ✅ | `Crud_PreservesCompleteDataAndAuditsEachMutation`, `AuditDoesNotCopyResponsibleContactInformation`, rollback SQL |
| Migration compatível com legado | ✅ | `Upgrade_PreservesLegacyRowsAndForeignKeys_GrantsPermissionsIdempotently` |
| Autorização e códigos HTTP | ✅ | Testes 200/201/400/401/403/404/409/503 e OpenAPI |
| Seções do formulário | DEPENDENTE DO FRONTEND | Contrato cobre identificação, empresa, endereço, contatos, responsáveis, regulatório e operação |
| Máscaras, loading, indicação de obrigatórios | DEPENDENTE DO FRONTEND | Backend normaliza/valida e retorna erros por campo |
| Ação explícita, pré-visualização, correção e alertas visuais | DEPENDENTE DO FRONTEND | Endpoints auxiliares não persistem; warnings/dados disponíveis no contrato |

## Arquivos alterados

Caminhos abaixo são relativos à raiz do repositório. Os grupos indicam o motivo de cada arquivo; nenhum arquivo de funcionário, setor ou escala de produção foi refatorado.

### Modelo existente, classificações, dados e invariantes/normalização

- [src/Sistema.Gestao.Empresarial.Domain/Organizacoes/CadastroBrasileiro.cs](../src/Sistema.Gestao.Empresarial.Domain/Organizacoes/CadastroBrasileiro.cs)
- [src/Sistema.Gestao.Empresarial.Domain/Organizacoes/CadastroUnidadeHospitalar.cs](../src/Sistema.Gestao.Empresarial.Domain/Organizacoes/CadastroUnidadeHospitalar.cs)
- [src/Sistema.Gestao.Empresarial.Domain/Organizacoes/ClassificacaoUnidadeHospitalar.cs](../src/Sistema.Gestao.Empresarial.Domain/Organizacoes/ClassificacaoUnidadeHospitalar.cs)
- [src/Sistema.Gestao.Empresarial.Domain/Organizacoes/UnidadeHospitalar.cs](../src/Sistema.Gestao.Empresarial.Domain/Organizacoes/UnidadeHospitalar.cs)

### Contratos, interfaces, permissões e FluentValidation

- [src/Sistema.Gestao.Empresarial.Application/Organizations/HospitalLookupContracts.cs](../src/Sistema.Gestao.Empresarial.Application/Organizations/HospitalLookupContracts.cs)
- [src/Sistema.Gestao.Empresarial.Application/Organizations/HospitalUnitRegistrationContracts.cs](../src/Sistema.Gestao.Empresarial.Application/Organizations/HospitalUnitRegistrationContracts.cs)
- [src/Sistema.Gestao.Empresarial.Application/Organizations/HospitalUnitValidators.cs](../src/Sistema.Gestao.Empresarial.Application/Organizations/HospitalUnitValidators.cs)
- [src/Sistema.Gestao.Empresarial.Application/Authorization/PermissionContracts.cs](../src/Sistema.Gestao.Empresarial.Application/Authorization/PermissionContracts.cs)
- [src/Sistema.Gestao.Empresarial.Application/Organizations/OrganizationCatalogContracts.cs](../src/Sistema.Gestao.Empresarial.Application/Organizations/OrganizationCatalogContracts.cs)
- [src/Sistema.Gestao.Empresarial.Application/Organizations/OrganizationCatalogValidators.cs](../src/Sistema.Gestao.Empresarial.Application/Organizations/OrganizationCatalogValidators.cs)

### Casos de uso, EF/migration, auditoria e clientes HTTP

- [src/Sistema.Gestao.Empresarial.Infrastructure/Configuration/HospitalLookupOptions.cs](../src/Sistema.Gestao.Empresarial.Infrastructure/Configuration/HospitalLookupOptions.cs)
- [src/Sistema.Gestao.Empresarial.Infrastructure/Organizations/BrasilApiCnpjLookupService.cs](../src/Sistema.Gestao.Empresarial.Infrastructure/Organizations/BrasilApiCnpjLookupService.cs)
- [src/Sistema.Gestao.Empresarial.Infrastructure/Organizations/HospitalLookupHttp.cs](../src/Sistema.Gestao.Empresarial.Infrastructure/Organizations/HospitalLookupHttp.cs)
- [src/Sistema.Gestao.Empresarial.Infrastructure/Organizations/OrganizationCatalogService.HospitalUnits.cs](../src/Sistema.Gestao.Empresarial.Infrastructure/Organizations/OrganizationCatalogService.HospitalUnits.cs)
- [src/Sistema.Gestao.Empresarial.Infrastructure/Organizations/ViaCepLookupService.cs](../src/Sistema.Gestao.Empresarial.Infrastructure/Organizations/ViaCepLookupService.cs)
- [src/Sistema.Gestao.Empresarial.Infrastructure/Persistence/Migrations/20260929110220_CompleteHospitalUnitRegistration.Designer.cs](../src/Sistema.Gestao.Empresarial.Infrastructure/Persistence/Migrations/20260929110220_CompleteHospitalUnitRegistration.Designer.cs)
- [src/Sistema.Gestao.Empresarial.Infrastructure/Persistence/Migrations/20260929110220_CompleteHospitalUnitRegistration.cs](../src/Sistema.Gestao.Empresarial.Infrastructure/Persistence/Migrations/20260929110220_CompleteHospitalUnitRegistration.cs)
- [src/Sistema.Gestao.Empresarial.Infrastructure/DependencyInjection.cs](../src/Sistema.Gestao.Empresarial.Infrastructure/DependencyInjection.cs)
- [src/Sistema.Gestao.Empresarial.Infrastructure/Organizations/OrganizationCatalogService.cs](../src/Sistema.Gestao.Empresarial.Infrastructure/Organizations/OrganizationCatalogService.cs)
- [src/Sistema.Gestao.Empresarial.Infrastructure/Persistence/AppDbContext.cs](../src/Sistema.Gestao.Empresarial.Infrastructure/Persistence/AppDbContext.cs)
- [src/Sistema.Gestao.Empresarial.Infrastructure/Persistence/Migrations/AdministratorProfilePermissionBackfill.cs](../src/Sistema.Gestao.Empresarial.Infrastructure/Persistence/Migrations/AdministratorProfilePermissionBackfill.cs)
- [src/Sistema.Gestao.Empresarial.Infrastructure/Persistence/Migrations/AppDbContextModelSnapshot.cs](../src/Sistema.Gestao.Empresarial.Infrastructure/Persistence/Migrations/AppDbContextModelSnapshot.cs)

### Endpoints autorizados e configuração de consultas auxiliares

- [src/Sistema.Gestao.Empresarial.Api/Controllers/HospitalUnitsController.cs](../src/Sistema.Gestao.Empresarial.Api/Controllers/HospitalUnitsController.cs)
- [src/Sistema.Gestao.Empresarial.Api/appsettings.json](../src/Sistema.Gestao.Empresarial.Api/appsettings.json)

### Cobertura e adaptação de testes às permissões/schema histórico

- [tests/Sistema.Gestao.Empresarial.IntegrationTests/Api/HospitalLookupApiTests.cs](../tests/Sistema.Gestao.Empresarial.IntegrationTests/Api/HospitalLookupApiTests.cs)
- [tests/Sistema.Gestao.Empresarial.IntegrationTests/Api/HospitalUnitsApiTests.cs](../tests/Sistema.Gestao.Empresarial.IntegrationTests/Api/HospitalUnitsApiTests.cs)
- [tests/Sistema.Gestao.Empresarial.IntegrationTests/Organizations/HospitalLookupServiceTests.cs](../tests/Sistema.Gestao.Empresarial.IntegrationTests/Organizations/HospitalLookupServiceTests.cs)
- [tests/Sistema.Gestao.Empresarial.IntegrationTests/Organizations/HospitalUnitServiceTests.cs](../tests/Sistema.Gestao.Empresarial.IntegrationTests/Organizations/HospitalUnitServiceTests.cs)
- [tests/Sistema.Gestao.Empresarial.IntegrationTests/RealInfrastructure/HospitalUnitDatabaseTests.cs](../tests/Sistema.Gestao.Empresarial.IntegrationTests/RealInfrastructure/HospitalUnitDatabaseTests.cs)
- [tests/Sistema.Gestao.Empresarial.UnitTests/Domain/UnidadeHospitalarTests.cs](../tests/Sistema.Gestao.Empresarial.UnitTests/Domain/UnidadeHospitalarTests.cs)
- [tests/Sistema.Gestao.Empresarial.IntegrationTests/RealInfrastructure/ProfessionalLevelMigrationTests.cs](../tests/Sistema.Gestao.Empresarial.IntegrationTests/RealInfrastructure/ProfessionalLevelMigrationTests.cs)
- [tests/Sistema.Gestao.Empresarial.IntegrationTests/Security/EndpointSecurityTests.cs](../tests/Sistema.Gestao.Empresarial.IntegrationTests/Security/EndpointSecurityTests.cs)

- [hospital-units.http](../hospital-units.http): exemplos dos oito endpoints e erro de validação.
- Este relatório: diagnóstico, decisões, contratos, evidências e itens de frontend.

## Pendências e fora de escopo

A interface, suas seções/máscaras/loading, aplicação das sugestões e alertas visuais pertencem ao frontend. Deploy/aplicação da migration no ambiente definitivo seguem o fluxo do PR; a execução nesta entrega migrou somente bancos temporários. Sem CRUDs de outros módulos, sem exclusão física e sem criação automática de usuários.

Branch: `feature/48-complete-hospital-unit-crud`; destino: `main`. Commit e URL do PR são informados na entrega final para evitar referências circulares dentro do próprio commit.
