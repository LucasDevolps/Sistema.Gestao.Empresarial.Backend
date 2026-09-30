using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Sistema.Gestao.Empresarial.Application.Employees;

namespace Sistema.Gestao.Empresarial.IntegrationTests.RealInfrastructure;

[Collection(RealInfrastructureCollection.Name)]
public sealed class EmployeeScalePersistenceTests(RealInfrastructureFixture fixture)
{
    [RealInfrastructureFact]
    [Trait("Category", "RealInfrastructure")]
    public async Task Migration_PreservesExistingEmployeesAndEnforcesProductivity()
    {
        await using var db = fixture.CreateDbContext();
        var service = fixture.CreateEmployeeService(db);
        var context = fixture.CreateOperationContext(Guid.NewGuid());
        var request = fixture.CreateEmployeeRequest($"scale-{Guid.NewGuid():N}@test.com",
            [new(fixture.ActingUnitGuid, new DateOnly(2026, 1, 2))],
            [new(fixture.SectorGuid, new DateOnly(2026, 1, 3))]);
        var before = await service.CreateAsync(request, context, default);
        var count = await db.Funcionarios.CountAsync();
        var migrator = db.GetService<IMigrator>();
        try
        {
            // The fixture owns a disposable test database; exercise rollback and upgrade.
            await migrator.MigrateAsync("20260929110220_CompleteHospitalUnitRegistration");
            await migrator.MigrateAsync();
            db.ChangeTracker.Clear();
            var after = await service.GetAsync(before.Guid, context, default);
            Assert.NotNull(after);
            Assert.Equal(count, await db.Funcionarios.CountAsync());
            Assert.Equal(before.RegistrationNumber, after.RegistrationNumber);
            Assert.Equal(before.HiringUnit, after.HiringUnit);
            Assert.Equal(before.Profession, after.Profession);
            Assert.Equal(before.Position, after.Position);
            Assert.Equal(before.Level, after.Level);
            Assert.Equal(before.ActingUnits, after.ActingUnits);
            Assert.Equal(before.Sectors, after.Sectors);
            Assert.Equal(0, after.Productivity);
            Assert.False(after.ParticipatesInSchedule);
            foreach (var productivity in new[] { -1, 3, 4, 99 })
            {
                var error = await Assert.ThrowsAsync<SqlException>(() => db.Database.ExecuteSqlInterpolatedAsync(
                    $"UPDATE sge.Funcionarios SET Produtividade = {productivity} WHERE Guid = {before.Guid}"));
                Assert.Equal(547, error.Number);
            }
            foreach (var productivity in new[] { 0, 1, 2 })
            {
                var updated = await service.UpdateAsync(before.Guid, new UpdateEmployeeRequest(
                    request.Name, request.Email, request.Phone, request.ProfessionGuid,
                    request.PositionGuid, request.LevelGuid, productivity, true), context, default);
                Assert.Equal(productivity, updated!.Productivity);
                Assert.True(updated.ParticipatesInSchedule);
            }
            var page = await service.ListAsync(new(null, true, null, ParticipatesInSchedule: true), context, default);
            Assert.Contains(page.Items, x => x.Guid == before.Guid && x.Productivity == 2);
            await service.UpdateAsync(before.Guid, new UpdateEmployeeRequest(
                request.Name, request.Email, request.Phone, request.ProfessionGuid,
                request.PositionGuid, request.LevelGuid, 0, false), context, default);
            db.ChangeTracker.Clear();
            var reset = await service.GetAsync(before.Guid, context, default);
            Assert.Equal(0, reset!.Productivity);
            Assert.False(reset.ParticipatesInSchedule);
        }
        finally
        {
            await migrator.MigrateAsync();
        }
    }
}
