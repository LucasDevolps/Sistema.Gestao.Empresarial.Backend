namespace Sistema.Gestao.Empresarial.IntegrationTests.RealInfrastructure;

[AttributeUsage(AttributeTargets.Method)]
public sealed class RealInfrastructureFactAttribute : FactAttribute
{
    public RealInfrastructureFactAttribute()
    {
        if (!string.Equals(
                Environment.GetEnvironmentVariable("SGE_REAL_INFRASTRUCTURE_TESTS"),
                "true",
                StringComparison.OrdinalIgnoreCase))
        {
            Skip = "Defina SGE_REAL_INFRASTRUCTURE_TESTS=true e inicie SQL Server, Redis e RabbitMQ.";
        }
    }
}
