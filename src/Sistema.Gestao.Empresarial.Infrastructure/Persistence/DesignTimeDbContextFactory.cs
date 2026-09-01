using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Sistema.Gestao.Empresarial.Infrastructure.Persistence;

public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("SGE_DESIGNTIME_SQLSERVER")
            ?? "Server=localhost;Database=SistemaGestaoEmpresarial;Integrated Security=true;TrustServerCertificate=true";
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer(connectionString)
            .Options;

        return new AppDbContext(options, TimeProvider.System);
    }
}
