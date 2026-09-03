using FluentValidation;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Sistema.Gestao.Empresarial.Application.Bootstrap;
using Sistema.Gestao.Empresarial.Infrastructure.Bootstrap;
using Sistema.Gestao.Empresarial.Infrastructure.Persistence;
using Sistema.Gestao.Empresarial.Infrastructure.Security;

namespace Sistema.Gestao.Empresarial.IntegrationTests.RealInfrastructure;

[Collection(RealInfrastructureCollection.Name)]
public sealed class InitialAdminBootstrapConcurrencyTests(RealInfrastructureFixture fixture)
{
    [RealInfrastructureFact]
    [Trait("Category", "RealInfrastructure")]
    public async Task ExecucoesConcorrentes_DevemProvisionarSomenteUmAdministrador()
    {
        var databaseName = $"sge_bootstrap_it_{Guid.NewGuid():N}";
        var sourceBuilder = new SqlConnectionStringBuilder(fixture.DatabaseConnectionString);
        var masterBuilder = new SqlConnectionStringBuilder(sourceBuilder.ConnectionString)
        {
            InitialCatalog = "master"
        };
        var databaseBuilder = new SqlConnectionStringBuilder(sourceBuilder.ConnectionString)
        {
            InitialCatalog = databaseName
        };

        await ExecuteDatabaseCommandAsync(
            masterBuilder.ConnectionString,
            $"CREATE DATABASE [{databaseName}]");
        try
        {
            await using (var migrationContext = CreateDbContext(databaseBuilder.ConnectionString))
            {
                await migrationContext.Database.MigrateAsync();
            }

            var first = ExecuteBootstrapAsync(databaseBuilder.ConnectionString, "primeiro@hospital.test");
            var second = ExecuteBootstrapAsync(databaseBuilder.ConnectionString, "segundo@hospital.test");
            var outcomes = await Task.WhenAll(first, second);

            Assert.Single(outcomes, outcome => outcome is null);
            Assert.Single(outcomes, outcome => outcome is InitialAdminAlreadyProvisionedException);

            await using var verificationContext = CreateDbContext(databaseBuilder.ConnectionString);
            Assert.Equal(1, await verificationContext.Usuarios.IgnoreQueryFilters().CountAsync());
            Assert.Equal(1, await verificationContext.Organizacoes.IgnoreQueryFilters().CountAsync());
            Assert.Equal(1, await verificationContext.AuditLogs.CountAsync());
            Assert.Equal(1, await verificationContext.OutboxMessages.CountAsync());
        }
        finally
        {
            await ExecuteDatabaseCommandAsync(
                masterBuilder.ConnectionString,
                $"""
                IF DB_ID(N'{databaseName}') IS NOT NULL
                BEGIN
                    ALTER DATABASE [{databaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
                    DROP DATABASE [{databaseName}];
                END
                """);
        }
    }

    private static async Task<Exception?> ExecuteBootstrapAsync(string connectionString, string email)
    {
        await using var dbContext = CreateDbContext(connectionString);
        var service = new InitialAdminBootstrapService(
            dbContext,
            new CredentialHasher(),
            new InitialAdminBootstrapRequestValidator(),
            TimeProvider.System);
        try
        {
            await service.ExecuteAsync(new InitialAdminBootstrapRequest(
                $"Organização {email}",
                "Hospital Central",
                "Administração Hospitalar",
                "Administrador do Sistema",
                "SR",
                "Administrador Inicial",
                email,
                null,
                new DateOnly(2026, 1, 1),
                "Uma-Senha-Forte-2026!"), CancellationToken.None);
            return null;
        }
        catch (Exception exception)
        {
            return exception;
        }
    }

    private static AppDbContext CreateDbContext(string connectionString) =>
        new(
            new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlServer(connectionString, sql =>
                    sql.EnableRetryOnFailure(5, TimeSpan.FromSeconds(2), null))
                .Options,
            TimeProvider.System);

    private static async Task ExecuteDatabaseCommandAsync(string connectionString, string commandText)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = commandText;
        await command.ExecuteNonQueryAsync();
    }
}
