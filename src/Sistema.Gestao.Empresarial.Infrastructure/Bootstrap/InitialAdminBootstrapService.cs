using System.Data;
using System.Text.Json;
using FluentValidation;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Sistema.Gestao.Empresarial.Application.Authentication;
using Sistema.Gestao.Empresarial.Application.Bootstrap;
using Sistema.Gestao.Empresarial.Application.Authorization;
using Sistema.Gestao.Empresarial.Domain.Auditoria;
using Sistema.Gestao.Empresarial.Domain.Integracao;
using Sistema.Gestao.Empresarial.Domain.Organizacoes;
using Sistema.Gestao.Empresarial.Domain.Pessoas;
using Sistema.Gestao.Empresarial.Domain.Seguranca;
using Sistema.Gestao.Empresarial.Infrastructure.Persistence;

namespace Sistema.Gestao.Empresarial.Infrastructure.Bootstrap;

public sealed class InitialAdminBootstrapService(
    AppDbContext dbContext,
    ICredentialHasher credentialHasher,
    IValidator<InitialAdminBootstrapRequest> validator,
    TimeProvider timeProvider) : IInitialAdminBootstrapService
{
    private const string AdministratorProfileName = "ADMINISTRADOR_INICIAL";
    private const string Producer = "Sistema.Gestao.Empresarial.Bootstrap";
    private static readonly SemaphoreSlim NonRelationalLock = new(1, 1);

    public async Task<InitialAdminBootstrapResult> ExecuteAsync(
        InitialAdminBootstrapRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        await validator.ValidateAndThrowAsync(request, cancellationToken);

        if (!dbContext.Database.IsRelational())
        {
            await NonRelationalLock.WaitAsync(cancellationToken);
            try
            {
                return await ExecuteOnceAsync(request, null, cancellationToken);
            }
            finally
            {
                NonRelationalLock.Release();
            }
        }

        var strategy = dbContext.Database.CreateExecutionStrategy();
        var attempt = 0;
        try
        {
            return await strategy.ExecuteAsync(async () =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (Interlocked.Increment(ref attempt) > 1)
                {
                    dbContext.ChangeTracker.Clear();
                }

                await using var transaction = await dbContext.Database.BeginTransactionAsync(
                    IsolationLevel.Serializable,
                    cancellationToken);
                await AcquireDatabaseLockAsync(cancellationToken);
                var result = await ExecuteOnceAsync(request, transaction, cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return result;
            });
        }
        catch (DbUpdateException exception) when (
            exception.InnerException is SqlException { Number: 2601 or 2627 })
        {
            throw new InitialAdminBootstrapConflictException(
                "O bootstrap conflitou com dados existentes ou outra execução concorrente.",
                exception);
        }
    }

    private async Task<InitialAdminBootstrapResult> ExecuteOnceAsync(
        InitialAdminBootstrapRequest request,
        IDbContextTransaction? transaction,
        CancellationToken cancellationToken)
    {
        if (await dbContext.Usuarios.IgnoreQueryFilters().AnyAsync(cancellationToken))
        {
            throw new InitialAdminAlreadyProvisionedException();
        }

        var permissions = await dbContext.Permissoes
            .Where(permission => permission.Ativo)
            .OrderBy(permission => permission.Id)
            .ToListAsync(cancellationToken);
        if (permissions.Count == 0)
        {
            throw new InvalidOperationException(
                "O catálogo de permissões está vazio. Aplique todas as migrations antes do bootstrap.");
        }

        var availablePermissionCodes = permissions
            .Select(permission => permission.Codigo)
            .ToHashSet(StringComparer.Ordinal);
        var missingPermissionCodes = PermissionCodes.All
            .Where(code => !availablePermissionCodes.Contains(code))
            .OrderBy(code => code, StringComparer.Ordinal)
            .ToArray();
        if (missingPermissionCodes.Length > 0)
        {
            throw new InvalidOperationException(
                $"O catálogo de permissões está incompleto: {string.Join(", ", missingPermissionCodes)}.");
        }

        var normalizedLevelCode = request.ProfessionalLevelCode.Trim().ToUpperInvariant();
        var level = await dbContext.NiveisProfissionais
            .SingleOrDefaultAsync(
                professionalLevel => professionalLevel.Codigo == normalizedLevelCode && professionalLevel.Ativo,
                cancellationToken)
            ?? throw new InvalidOperationException(
                $"O nível profissional '{normalizedLevelCode}' não existe ou está inativo.");

        var now = timeProvider.GetUtcNow();
        if (request.AdmissionDate > DateOnly.FromDateTime(now.UtcDateTime))
        {
            throw new ValidationException("A data de admissão não pode estar no futuro.");
        }

        var organization = new Organizacao(Guid.NewGuid(), request.OrganizationName, now);
        dbContext.Organizacoes.Add(organization);
        await dbContext.SaveChangesAsync(cancellationToken);

        var hospitalUnit = new UnidadeHospitalar(
            Guid.NewGuid(), organization.Id, request.HospitalUnitName, now);
        var profession = new Profissao(Guid.NewGuid(), request.ProfessionName, "Criada pelo bootstrap inicial.", now);
        var position = new Cargo(Guid.NewGuid(), request.PositionName, "Criado pelo bootstrap inicial.", now);
        dbContext.AddRange(hospitalUnit, profession, position);
        await dbContext.SaveChangesAsync(cancellationToken);

        var employee = new Funcionario(
            Guid.NewGuid(),
            request.AdministratorName,
            request.AdministratorEmail,
            request.AdministratorPhone,
            profession.Id,
            position.Id,
            level.Id,
            hospitalUnit.Id,
            request.AdmissionDate,
            now);
        dbContext.Funcionarios.Add(employee);
        await dbContext.SaveChangesAsync(cancellationToken);

        var user = new Usuario(
            Guid.NewGuid(),
            employee.Id,
            request.AdministratorEmail,
            credentialHasher.HashPassword(request.Password),
            now);
        var profile = new Perfil(
            Guid.NewGuid(),
            AdministratorProfileName,
            "Perfil integral criado exclusivamente pelo bootstrap inicial.",
            now);
        dbContext.AddRange(user, profile);
        await dbContext.SaveChangesAsync(cancellationToken);

        dbContext.UsuariosPerfis.Add(new UsuarioPerfil(Guid.NewGuid(), user.Id, profile.Id, now));
        dbContext.PerfisPermissoes.AddRange(permissions.Select(permission =>
            new PerfilPermissao(Guid.NewGuid(), profile.Id, permission.Id, now)));
        AddAuditAndOutbox(organization, hospitalUnit, employee, user, profile, permissions.Count, now);
        await dbContext.SaveChangesAsync(cancellationToken);

        if (transaction is null)
        {
            // The in-memory provider has no transactions; all writes above are still serialized for tests.
            dbContext.ChangeTracker.DetectChanges();
        }

        return new InitialAdminBootstrapResult(
            organization.Guid,
            hospitalUnit.Guid,
            employee.Guid,
            user.Guid,
            profile.Guid,
            permissions.Count);
    }

    private async Task AcquireDatabaseLockAsync(CancellationToken cancellationToken)
    {
        if (!dbContext.Database.IsSqlServer())
        {
            return;
        }

        const string sql = """
            DECLARE @result int;
            EXEC @result = sys.sp_getapplock
                @Resource = N'SGE_INITIAL_ADMIN_BOOTSTRAP',
                @LockMode = N'Exclusive',
                @LockOwner = N'Transaction',
                @LockTimeout = 15000;
            SELECT @result AS [Value];
            """;
        var connection = dbContext.Database.GetDbConnection();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Transaction = dbContext.Database.CurrentTransaction?.GetDbTransaction();
        var scalar = await command.ExecuteScalarAsync(cancellationToken);
        var lockResult = Convert.ToInt32(scalar, System.Globalization.CultureInfo.InvariantCulture);
        if (lockResult < 0)
        {
            throw new TimeoutException("Não foi possível adquirir o lock exclusivo do bootstrap inicial.");
        }
    }

    private void AddAuditAndOutbox(
        Organizacao organization,
        UnidadeHospitalar hospitalUnit,
        Funcionario employee,
        Usuario user,
        Perfil profile,
        int permissionCount,
        DateTimeOffset now)
    {
        var correlationId = Guid.NewGuid();
        var traceId = $"bootstrap-{correlationId:N}";
        var data = new
        {
            organizationGuid = organization.Guid,
            hospitalUnitGuid = hospitalUnit.Guid,
            employeeGuid = employee.Guid,
            userGuid = user.Guid,
            profileGuid = profile.Guid,
            grantedPermissionCount = permissionCount
        };

        dbContext.AuditLogs.Add(new AuditLog(
            Guid.NewGuid(),
            "Usuario",
            user.Guid,
            "ADMINISTRADOR_INICIAL_CRIADO",
            user.Guid,
            now,
            correlationId,
            traceId,
            null,
            null,
            JsonSerializer.Serialize(data)));

        var eventId = Guid.NewGuid();
        var messageId = Guid.NewGuid();
        var envelope = new
        {
            eventId,
            messageId,
            eventType = "AdministradorInicialCriado",
            eventVersion = 1,
            correlationId,
            traceId,
            occurredAt = now,
            producer = Producer,
            data
        };
        dbContext.OutboxMessages.Add(new OutboxMessage(
            Guid.NewGuid(),
            messageId,
            eventId,
            "AdministradorInicialCriado",
            1,
            JsonSerializer.Serialize(envelope),
            correlationId,
            traceId,
            Producer,
            now));
    }
}

public sealed class InitialAdminBootstrapConflictException(string message, Exception innerException)
    : Exception(message, innerException);
