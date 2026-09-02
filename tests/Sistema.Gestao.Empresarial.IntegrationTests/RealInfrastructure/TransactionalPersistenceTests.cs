using Microsoft.EntityFrameworkCore;

namespace Sistema.Gestao.Empresarial.IntegrationTests.RealInfrastructure;

[Collection(RealInfrastructureCollection.Name)]
public sealed class TransactionalPersistenceTests(RealInfrastructureFixture fixture)
{
    [RealInfrastructureFact]
    [Trait("Category", "RealInfrastructure")]
    public async Task FalhaNaOutbox_DeveReverterFuncionarioEAuditoria()
    {
        const string triggerName = "TR_IntegrationTest_RejectOutbox";
        var email = $"rollback-{fixture.IsolationKey}@hospital.test";
        var correlationId = Guid.NewGuid();
        await using (var setup = fixture.CreateDbContext())
        {
            await setup.Database.ExecuteSqlRawAsync($"""
                CREATE OR ALTER TRIGGER [sge].[{triggerName}]
                ON [sge].[OutboxMessages]
                AFTER INSERT
                AS
                BEGIN
                    THROW 51000, 'Falha de Outbox induzida pelo teste.', 1;
                END
                """);
        }

        try
        {
            await using var operationDb = fixture.CreateDbContext();
            await Assert.ThrowsAnyAsync<Exception>(() =>
                fixture.CreateEmployeeService(operationDb).CreateAsync(
                    fixture.CreateEmployeeRequest(email),
                    fixture.CreateOperationContext(correlationId),
                    CancellationToken.None));
        }
        finally
        {
            await using var cleanup = fixture.CreateDbContext();
            await cleanup.Database.ExecuteSqlRawAsync($"DROP TRIGGER [sge].[{triggerName}]");
        }

        await using var verification = fixture.CreateDbContext();
        Assert.False(await verification.Funcionarios.AnyAsync(x => x.Email == email));
        Assert.False(await verification.AuditLogs.AnyAsync(x => x.CorrelationId == correlationId));
        Assert.False(await verification.OutboxMessages.AnyAsync(x => x.CorrelationId == correlationId));
    }
}
