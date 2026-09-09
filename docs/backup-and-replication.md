# Persistência, replicação e backup do SQL Server

Este documento consolida a resiliência de dados do Sistema de Gestão Empresarial:
persistência dos arquivos do banco, réplica de espera síncrona, backup completo
verificável, cópia semanal automatizada para o Windows, retenção, restauração,
segurança e observabilidade. Complementa
[`production-readiness.md`](production-readiness.md), que mantém o checklist de
release e a política de retenção de auditoria.

A topologia aprovada continua sendo de host único. A réplica reduz a janela de
perda em falha do primário e habilita failover **manual**; ela **não substitui o
backup**.

---

## 1. Persistência do SQL Server

O serviço `sqlserver` do [`docker-compose.yml`](../docker-compose.yml) grava todo
o estado (`/var/opt/mssql`, incluindo `data`, `log` e `backup`) no volume nomeado
`sql-data`. Volumes nomeados vivem fora do ciclo de vida do container: parar,
remover e recriar o container preserva o banco. Não há porta de SQL Server
publicada no host em nenhum arquivo Compose de produção; o acesso é apenas pelas
redes internas do Compose.

### Operações Docker e seu efeito no volume

| Operação | Efeito em `sql-data` |
| --- | --- |
| `docker compose stop` / `start` / `restart` | preserva |
| `docker compose down` | preserva (remove apenas containers e redes) |
| `docker compose up --build` / recriação de container | preserva |
| `docker compose down -v` | **APAGA** o volume e o banco |
| `docker volume rm sistema-gestao-empresarial_sql-data` | **APAGA** |
| `docker volume prune` (com o volume não referenciado) | **APAGA** |
| `docker system prune --volumes` | **APAGA** |

`scripts/dev-down.ps1 -Down -Volumes` é o único caminho dos scripts do repositório
que remove volumes e exige a confirmação literal `sim`.

### Verificação da persistência

```bash
docker compose exec -T sqlserver /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa \
  -P "$SGE_SQLSERVER_SA_PASSWORD" -C -Q "CREATE TABLE dbo._persist_check(id int); DROP TABLE dbo._persist_check;"
docker compose up -d --force-recreate --no-deps sqlserver
# Após o healthcheck ficar saudável, confirmar que o banco e as migrations continuam presentes:
bash scripts/_db-has-schema.sh
```

O recreate do container mantém o volume; `_db-has-schema.sh` deve reportar
`HAS_SCHEMA`.

---

## 2. Réplica de espera síncrona

### Topologia

O overlay opcional [`docker-compose.replica.yml`](../docker-compose.replica.yml)
adiciona:

- `sqlserver-replica`: segunda instância com o mesmo digest de imagem do primário
  e volume nomeado **independente** `sql-replica-data`;
- `MSSQL_ENABLE_HADR=1` no primário e na réplica (aplicar o overlay recria o
  container `sqlserver` uma vez);
- comunicação apenas pela rede interna `sge-network`. O endpoint de espelhamento
  (TCP 5022) não é publicado no host — **nenhuma porta nova**.

O mecanismo é o **Always On Availability Group** nativo em modo *read-scale*
(`CLUSTER_TYPE = NONE`), com `AVAILABILITY_MODE = SYNCHRONOUS_COMMIT`,
`FAILOVER_MODE = MANUAL` e `SEEDING_MODE = AUTOMATIC`. É a configuração suportada
para SQL Server em Linux/containers sem um gerenciador de cluster (Pacemaker/WSFC).
Failover automático não é prometido: não há árbitro nem *fencing*.

### Provisionamento

```bash
# 1. Definir no .env: SGE_SQLSERVER_REPLICA_SA_PASSWORD e SGE_AG_CERTIFICATE_PASSWORD
# 2. Subir a pilha com o overlay (sem o override local de desenvolvimento):
docker compose -f docker-compose.yml -f docker-compose.replica.yml up -d --build

# 3. Configurar o grupo de disponibilidade (idempotente):
wsl bash scripts/configure-availability-group.sh
```

`scripts/configure-availability-group.sh` cria a master key e os certificados de
endpoint em cada instância, troca os certificados públicos (arquivos temporários
removidos ao final), cria os endpoints `Hadr_endpoint`, cria o AG `SGE_AG`, faz a
réplica entrar no grupo, garante `RECOVERY FULL`, gera o backup base e adiciona
`SistemaGestaoEmpresarial` ao grupo. O *automatic seeding* transmite o banco para
a réplica.

### Verificação da sincronização

```bash
wsl bash scripts/verify-replica-sync.sh          # saída legível
wsl bash scripts/verify-replica-sync.sh --json   # 1 linha JSON para coleta
```

Reporta `synchronization_state_desc`, `synchronization_health_desc`,
`last_hardened_lsn`, filas de envio de log e de redo e o último commit aplicado na
réplica. **Sai com 0 apenas quando o estado é `SYNCHRONIZED` e a saúde é
`HEALTHY`**; sai com 2 quando dessincronizado e 1 em erro de execução. Consultas
equivalentes diretas:

```sql
SELECT ag.name, ar.replica_server_name, ars.role_desc,
       drs.synchronization_state_desc, drs.synchronization_health_desc,
       drs.last_hardened_lsn, drs.log_send_queue_size, drs.redo_queue_size
FROM sys.dm_hadr_database_replica_states drs
JOIN sys.availability_replicas ar   ON ar.replica_id = drs.replica_id
JOIN sys.dm_hadr_availability_replica_states ars ON ars.replica_id = drs.replica_id
JOIN sys.availability_groups ag     ON ag.group_id = ar.group_id;
```

### Procedimento de failover — MANUAL

Failover é sempre uma decisão humana registrada. Não há promoção automática.

**A) Failover planejado sem perda de dados** (primário acessível, réplica
`SYNCHRONIZED`/`HEALTHY`):

1. Suspender gravações da aplicação (retirar API/Worker do fluxo).
2. Confirmar sincronização: `scripts/verify-replica-sync.sh` deve sair com 0.
3. Na **réplica**, promover:
   ```sql
   ALTER AVAILABILITY GROUP [SGE_AG] FORCE_FAILOVER_ALLOW_DATA_LOSS;
   ```
   Em *read-scale AG* toda promoção usa esta sintaxe; com a réplica sincronizada
   e saudável a promoção não perde dados confirmados.
4. Repontar a aplicação para a nova instância primária (atualizar
   `ConnectionStrings__SqlServer` para `sqlserver-replica,1433`) e retomar o
   tráfego.
5. Quando o antigo primário voltar, reconciliá-lo como réplica:
   ```sql
   ALTER DATABASE [SistemaGestaoEmpresarial] SET HADR RESUME;
   ```
   Se o banco tiver divergido, remover do AG e refazer o seeding.

**B) Failover forçado após perda do primário** (primário irrecuperável):

1. Confirmar que o primário não retornará em janela aceitável.
2. Na réplica, executar o mesmo `FORCE_FAILOVER_ALLOW_DATA_LOSS`. Transações não
   endurecidas na réplica no momento da falha são perdidas — quantificar pela
   última `last_hardened_lsn`/`last_commit_time` conhecida.
3. Repontar a aplicação e retomar.
4. Recriar uma nova réplica de espera a partir de backup + seeding antes de
   considerar o ambiente novamente resiliente.

Registrar em todos os casos: responsável, horário, motivo, estado de
sincronização observado e itens de reconciliação pendentes.

---

## 3. Backup completo verificável

`scripts/backup-and-verify-sqlserver.sh` (execução com a pilha SQL saudável):

```powershell
wsl --cd "C:\caminho\do\repositorio" bash scripts/backup-and-verify-sqlserver.sh
```

Em uma execução o script:

1. Recusa-se a rodar se já existir `.bak`/`.sha256` com o mesmo nome (o nome usa
   timestamp UTC ao segundo) — **nunca sobrescreve** um backup existente.
2. `BACKUP DATABASE ... WITH COPY_ONLY, CHECKSUM, INIT` para
   `artifacts/backups/SistemaGestaoEmpresarial-<UTC>.bak`.
3. `RESTORE VERIFYONLY ... WITH CHECKSUM`.
4. Copia o `.bak` para o host e grava `<arquivo>.bak.sha256` (SHA-256).
5. Restaura em um banco temporário de nome controlado e roda
   `DBCC CHECKDB (...) WITH NO_INFOMSGS, DATA_PURITY`, removendo apenas o banco
   temporário.
6. Qualquer falha aborta com código diferente de zero (`set -euo pipefail`,
   `sqlcmd -b`) e grava um registro `failure` no JSONL.

`SGE_BACKUP_DIRECTORY` redireciona a saída; `SGE_BACKUP_LOG_FILE` redireciona o
JSONL (padrão `logs/backup-sqlserver.jsonl`). `artifacts/` e `logs/` estão no
`.gitignore` — backups e logs **não entram no Git**.

Um backup que permanece apenas no mesmo host não atende recuperação de desastre:
a etapa 4 da seção seguinte move a cópia validada para fora do host.

---

## 4. Cópia semanal automatizada para o Windows

`scripts/copy-backups-to-windows.ps1` (PowerShell 7) leva o backup validado para
**um ou mais** armazenamentos Windows independentes do host de containers. Por
padrão replica em **dois discos** (`C:` e `D:`) para redundância: se um disco
parar, o outro ainda recebe a cópia da semana.

```powershell
# Execução manual (gera o backup no WSL e copia):
pwsh ./scripts/copy-backups-to-windows.ps1 -RunBackup

# Registrar a tarefa semanal no Task Scheduler (domingo 03:00 por padrão):
pwsh ./scripts/copy-backups-to-windows.ps1 -Register
pwsh ./scripts/copy-backups-to-windows.ps1 -Register -ScheduleDay Saturday -ScheduleTime 02:30
```

Fluxo por execução:

1. Com `-RunBackup`, executa `scripts/backup-and-verify-sqlserver.sh` no WSL.
2. Seleciona o par `.bak` + `.sha256` mais recente em `artifacts/backups`.
3. Revalida o SHA-256 na origem; divergência aborta (nenhum destino é tocado).
4. Descarta destinos cuja unidade não existe (aviso `unidade ausente`), desde que
   reste ao menos um destino utilizável.
5. Para **cada** destino restante:
   a. Garante o diretório com **ACL restritiva** (SIDs, sempre resolvíveis):
      herança desabilitada, apenas `S-1-5-18` (SYSTEM), `S-1-5-32-544`
      (Administradores) e o SID do usuário atual com controle total; sem `Users`.
   b. Copia `.bak` e `.sha256` e **revalida o SHA-256 no destino**; falha nesse
      destino remove a cópia parcial e não interrompe os demais.
   c. **Só depois** de uma cópia nova validada, aplica a retenção: mantém as
      `-RetainCount` mais recentes (padrão **2**) e remove as demais, em par
      `.bak` + `.sha256`.
   d. Grava um registro JSONL por destino em `logs/backup-windows-copy.jsonl`.
6. Grava um registro `event_kind: "summary"` com os destinos OK, os que falharam e
   os pulados por unidade ausente.
7. **Resultado:** sucesso se ao menos um destino recebeu cópia validada. Se algum
   destino falhou (mas outro funcionou), o script conclui os demais e então
   termina com erro — a redundância é preservada e a falha fica visível no
   resultado da tarefa agendada. Falha em todos os destinos aborta.

### Destinos e diversidade de disco

Resolução dos destinos (`-Destinations`, aceita vários):

1. `-Destinations` / `-Destination` na linha de comando; senão
2. `SGE_BACKUP_WINDOWS_DESTINATION`, lista separada por `;`; senão
3. **padrão redundante**:
   - `C:\ProgramData\SistemaGestaoEmpresarial\Backups\SQLServer`
   - `D:\Backups\SistemaGestaoEmpresarial\SQLServer`

Para destino remoto, aponte para um compartilhamento **já autenticado e
criptografado** (SMB 3 com criptografia ou equivalente); o script não trafega
credenciais. `-RetainCount` aplica-se por destino.

### Tarefa agendada

`-Register` cria/atualiza a tarefa **"SGE - Copia semanal de backup SQL Server"**:
gatilho semanal, `pwsh -File copy-backups-to-windows.ps1 -RunBackup -RetainCount 2`,
principal com o usuário atual (`LogonType S4U`, `RunLevel Highest`),
`StartWhenAvailable` e limite de execução de 2 horas. Por padrão a tarefa **não
fixa destinos** — usa a resolução padrão do script (redundância `C:` + `D:`); para
travar outros destinos, rode `-Register` junto com `-Destinations`. Inspeção e
remoção:

```powershell
Get-ScheduledTask -TaskName 'SGE - Copia semanal de backup SQL Server' | Get-ScheduledTaskInfo
Unregister-ScheduledTask -TaskName 'SGE - Copia semanal de backup SQL Server' -Confirm:$false
```

---

## 5. Restauração

### Restauração de produção a partir de um `.bak`

```bash
# .bak e .sha256 já presentes em /var/opt/mssql/backup dentro do container.
docker compose exec -T sqlserver bash -euc '
  f=/var/opt/mssql/backup/SistemaGestaoEmpresarial-<UTC>.bak
  /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "$MSSQL_SA_PASSWORD" -C -b -Q "
    RESTORE VERIFYONLY FROM DISK = N'"'"'$f'"'"' WITH CHECKSUM;
    ALTER DATABASE [SistemaGestaoEmpresarial] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
    RESTORE DATABASE [SistemaGestaoEmpresarial] FROM DISK = N'"'"'$f'"'"' WITH REPLACE, CHECKSUM, RECOVERY;
    ALTER DATABASE [SistemaGestaoEmpresarial] SET MULTI_USER;"
'
```

Antes do `RESTORE`, conferir o SHA-256 do arquivo copiado contra o `.sha256`
correspondente. Se o banco fizer parte do AG, removê-lo do grupo antes de
restaurar e re-adicioná-lo depois.

### Teste de restauração (sem tocar o banco em uso)

O próprio `scripts/backup-and-verify-sqlserver.sh` **já executa um teste de
restauração completo** a cada run: restaura em um banco temporário isolado e roda
`DBCC CHECKDB`. Rodar o script é o teste de restauração comprovável; o resultado
fica no JSONL (`dbcc_checkdb: true`, `result: success`).

Para validar uma cópia específica já no destino Windows:

```powershell
$bak = 'D:\Backups\SistemaGestaoEmpresarial\SQLServer\SistemaGestaoEmpresarial-<UTC>.bak'
(Get-FileHash $bak -Algorithm SHA256).Hash.ToLower()
Get-Content "$bak.sha256" -TotalCount 1   # comparar o primeiro token
```

---

## 6. Segurança

- **Sem segredos no repositório.** `.env`, variantes locais e
  `CREDENCIAIS-DEV-LOCAL.md` estão fora do Git e do contexto de build Docker.
  `.env.example` só lista nomes e placeholders.
- **Sem senha `sa` em scripts.** Os scripts leem as senhas do `.env` em tempo de
  execução e as repassam por `--env`/variável de ambiente; nunca as imprimem e
  nunca as gravam nos JSONL. A senha da réplica é distinta da do primário.
- **ACL restritiva** no diretório de backup Windows (seção 4, passo 4).
- **Transporte** para destino remoto: apenas compartilhamento autenticado e
  criptografado; nenhuma credencial passa pelo script.
- **Sem portas novas.** O endpoint do AG (5022) existe apenas na rede interna do
  Compose; nenhum arquivo Compose publica SQL Server no host.
- Backups **não** são versionados (`artifacts/` no `.gitignore`).

---

## 7. Observabilidade

Dois arquivos JSONL, uma linha por evento, **sem dados sensíveis**:

| Arquivo | Origem | Campos principais |
| --- | --- | --- |
| `logs/backup-sqlserver.jsonl` | `backup-and-verify-sqlserver.sh` | `timestamp`, `result`, `stage`, `backup_file`, `size_bytes`, `sha256`, `verifyonly`, `dbcc_checkdb` |
| `logs/backup-windows-copy.jsonl` | `copy-backups-to-windows.ps1` | por destino: `timestamp`, `result`, `backup_file`, `source_path`, `destination_path`, `size_bytes`, `sha256`, `retain_count`, `copies_retained`, `copies_removed`, `error`; e um `event_kind: "summary"` com `destinations_ok`, `destinations_failed`, `skipped_missing_drive` |

Estado de replicação sob demanda via `scripts/verify-replica-sync.sh --json`
(`synchronization_state`, `synchronization_health`, `last_hardened_lsn`,
`log_send_queue_kb`, `redo_queue_kb`, `last_commit_time`). Eventos de falha
aparecem como `result: "failure"` com o `stage`/`error` correspondente. Os
arquivos ficam em `logs/`, fora do Git; encaminhe-os ao coletor operacional se
houver retenção central.

---

## 8. Escopo

**Incluído:** persistência dos arquivos do banco, réplica de espera com
armazenamento independente, verificação de sincronização, failover manual
documentado, backup `.bak` com checksum + SHA-256 + `RESTORE VERIFYONLY` +
`DBCC CHECKDB`, cópia semanal para Windows com Task Scheduler, retenção de 2
cópias com validação antes da exclusão, documentação e teste de restauração,
observabilidade sem segredos.

**Fora de escopo:** mudanças de regra de negócio ou de endpoints; armazenar
backups no Git; tratar a réplica como substituta do backup; failover automático
(exigiria árbitro/*fencing* e nova aprovação de topologia).
