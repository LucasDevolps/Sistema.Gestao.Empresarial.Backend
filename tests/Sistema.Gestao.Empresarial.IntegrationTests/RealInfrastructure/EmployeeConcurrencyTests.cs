using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Sistema.Gestao.Empresarial.Application.Employees;
using Sistema.Gestao.Empresarial.Domain.Common;
using Sistema.Gestao.Empresarial.Infrastructure.Employees;

namespace Sistema.Gestao.Empresarial.IntegrationTests.RealInfrastructure;

[Collection(RealInfrastructureCollection.Name)]
public sealed partial class EmployeeConcurrencyTests(RealInfrastructureFixture fixture)
{
    [RealInfrastructureFact]
    [Trait("Category", "RealInfrastructure")]
    public async Task CriacoesConcorrentes_DevemGerarMatriculasUnicasPelaSequence()
    {
        const int count = 12;
        var correlations = Enumerable.Range(0, count).Select(_ => Guid.NewGuid()).ToArray();
        var tasks = correlations.Select((correlationId, index) =>
            CreateEmployeeAsync($"concorrente-{fixture.IsolationKey}-{index}@hospital.test", correlationId));

        var employees = await Task.WhenAll(tasks);

        Assert.Equal(count, employees.Select(x => x.RegistrationNumber).Distinct().Count());
        Assert.All(employees, employee =>
            Assert.Matches(RegistrationNumberPattern(), employee.RegistrationNumber));
        await using var db = fixture.CreateDbContext();
        Assert.Equal(
            count,
            await db.OutboxMessages.CountAsync(x => correlations.Contains(x.CorrelationId)));
        Assert.Equal(
            count,
            await db.AuditLogs.CountAsync(x => correlations.Contains(x.CorrelationId)));
    }

    [RealInfrastructureFact]
    [Trait("Category", "RealInfrastructure")]
    public async Task MesmoEmailConcorrente_DevePersistirSomenteUmFuncionario()
    {
        var email = $"email-unico-{fixture.IsolationKey}@hospital.test";
        var attempts = await Task.WhenAll(
            TryCreateEmployeeAsync(email, Guid.NewGuid()),
            TryCreateEmployeeAsync(email, Guid.NewGuid()));

        Assert.Single(attempts, x => x.Employee is not null);
        Assert.Single(attempts, x => x.Error is DomainException or EmployeePersistenceConflictException);
        await using var db = fixture.CreateDbContext();
        Assert.Equal(1, await db.Funcionarios.CountAsync(x => x.Email == email));
    }

    [RealInfrastructureFact]
    [Trait("Category", "RealInfrastructure")]
    public async Task MesmoVinculoConcorrente_DeveManterSomenteUmaUnidadeAtiva()
    {
        var employee = await CreateEmployeeAsync(
            $"vinculo-{fixture.IsolationKey}@hospital.test",
            Guid.NewGuid());
        var startDate = new DateOnly(2026, 2, 1);
        var attempts = await Task.WhenAll(
            TryAddActingUnitAsync(employee.Guid, startDate),
            TryAddActingUnitAsync(employee.Guid, startDate));

        Assert.Single(attempts, x => x.Relationship is not null);
        Assert.Single(attempts, x => x.Error is DomainException or EmployeePersistenceConflictException);
        await using var db = fixture.CreateDbContext();
        var employeeId = await db.Funcionarios
            .Where(x => x.Guid == employee.Guid)
            .Select(x => x.Id)
            .SingleAsync();
        Assert.Equal(
            1,
            await db.FuncionariosUnidadesAtuacao.CountAsync(x => x.FuncionarioId == employeeId && x.Ativo));
    }

    private async Task<EmployeeResponse> CreateEmployeeAsync(string email, Guid correlationId)
    {
        await using var db = fixture.CreateDbContext();
        return await fixture.CreateEmployeeService(db).CreateAsync(
            fixture.CreateEmployeeRequest(email),
            fixture.CreateOperationContext(correlationId),
            CancellationToken.None);
    }

    private async Task<CreateAttempt> TryCreateEmployeeAsync(string email, Guid correlationId)
    {
        try
        {
            return new CreateAttempt(await CreateEmployeeAsync(email, correlationId), null);
        }
        catch (Exception exception)
        {
            return new CreateAttempt(null, exception);
        }
    }

    private async Task<RelationshipAttempt> TryAddActingUnitAsync(Guid employeeGuid, DateOnly startDate)
    {
        try
        {
            await using var db = fixture.CreateDbContext();
            var relationship = await fixture.CreateEmployeeService(db).AddActingUnitAsync(
                employeeGuid,
                new AddEmployeeActingUnitRequest(fixture.ActingUnitGuid, startDate),
                fixture.CreateOperationContext(Guid.NewGuid()),
                CancellationToken.None);
            return new RelationshipAttempt(relationship, null);
        }
        catch (Exception exception)
        {
            return new RelationshipAttempt(null, exception);
        }
    }

    private sealed record CreateAttempt(EmployeeResponse? Employee, Exception? Error);
    private sealed record RelationshipAttempt(EmployeeActingUnitResponse? Relationship, Exception? Error);

    [GeneratedRegex("^FUN[0-9]{9}$", RegexOptions.CultureInvariant)]
    private static partial Regex RegistrationNumberPattern();
}
