# Preparação para produção e operação local

## Escopo validado

A topologia aprovada é de host único. No ambiente local, o Nginx é a única entrada
e publica apenas `127.0.0.1:8080` e `127.0.0.1:8443`. API, Worker, SQL Server,
Redis, RabbitMQ e OpenTelemetry permanecem nas redes privadas do Compose. Se algum
desses componentes for movido para outro host, este documento deixa de autorizar a
topologia: TLS/mTLS, firewall e certificados emitidos pela infraestrutura deverão
ser definidos antes do deploy.

O localhost funciona como staging operacional deste projeto. Os testes automatizados
continuam sendo a evidência de isolamento entre duas organizações, inclusive acessos
negativos e mutações cross-tenant. Isso não equivale a um pentest independente.

## Retenção de auditoria

A política técnica padrão é:

| Registro | Retenção | Destino após o prazo |
| --- | ---: | --- |
| `ApiRequestLogs` | 180 dias | exclusão em lotes |
| `AuditLogs` de negócio | 1.825 dias (5 anos) | exclusão em lotes |

O Worker executa a rotina na inicialização e a cada 24 horas. Tamanho de lote,
quantidade máxima de lotes e prazos são configuráveis em `AuditRetention`. A rotina
não alcança prontuários clínicos, Inbox, Outbox, mensagens pendentes nem tabelas de
domínio.

Os 180 dias adotam o prazo de registros de acesso do art. 15 do Marco Civil da
Internet. Os cinco anos são uma decisão conservadora para auditoria de negócio e
exercício regular de direitos, não um prazo geral imposto pela LGPD. A LGPD exige
necessidade/finalidade e eliminação ao término do tratamento, ressalvadas as hipóteses
do art. 16. Se o sistema passar a armazenar prontuários de pacientes, eles deverão
receber classificação e ciclo próprios: a Lei 13.787/2018 prevê prazo mínimo de 20
anos a partir do último registro. O controlador e seu encarregado devem revisar esta
matriz quando finalidades, contratos ou regulações setoriais mudarem.

## Backup e restauração

Execute com a pilha SQL saudável:

```powershell
wsl --cd "C:\caminho\do\repositorio" bash scripts/backup-and-verify-sqlserver.sh
```

O script cria um backup `COPY_ONLY` com checksum em `artifacts/backups`, grava o
SHA-256, executa `RESTORE VERIFYONLY`, restaura em um banco temporário com nome
controlado, roda `DBCC CHECKDB` e remove somente o banco temporário. O backup deve
ser copiado para armazenamento criptografado, versionado e com acesso segregado.
Uma cópia que permanece apenas no mesmo host não atende recuperação de desastre.

## Checklist de release

1. Revisar o diff e obter aprovação independente.
2. Executar restore bloqueado, formatação, build, testes rápidos e testes
   `RealInfrastructure`.
3. Validar os manifests Compose e confirmar que somente o Nginx possui bindings.
4. Gerar e arquivar o SBOM CycloneDX; bloquear vulnerabilidades High/Critical corrigíveis.
5. Executar o backup com restauração e `DBCC CHECKDB`.
6. Usar certificado de CA confiável no overlay de produção; nunca habilitar o
   certificado autogerado fora do localhost.
7. Rotacionar credenciais pelo secret manager do ambiente e nunca por commit.
8. Confirmar alertas para falhas de login, HTTP 429/5xx, auditoria descartada,
   Outbox atrasada, Inbox/DLQ, recursos e expiração de certificados.
9. Registrar responsável, janela, rollback e evidências da mudança.

## Resposta e recuperação

- Suspeita de credencial: retirar o serviço do edge, rotacionar JWT, SQL, Redis e
  RabbitMQ, revogar sessões, preservar evidências e revisar auditoria.
- Falha de migration: não iniciar múltiplos jobs; restaurar backup somente após
  confirmar compatibilidade e registrar a decisão.
- Crescimento de DLQ/Outbox: interromper reprocessamento automático, classificar a
  causa, corrigir o consumidor e reprocessar preservando `MessageId`.
- Incidente de dados pessoais: acionar imediatamente controlador/encarregado e o
  processo jurídico aplicável; não apagar evidências fora da política aprovada.
