using System.Globalization;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Sistema.Gestao.Empresarial.Application.Bootstrap;
using Sistema.Gestao.Empresarial.Infrastructure.Bootstrap;
using Sistema.Gestao.Empresarial.Infrastructure.Persistence;
using Sistema.Gestao.Empresarial.Infrastructure.Security;

const int configurationErrorExitCode = 2;
const int alreadyProvisionedExitCode = 3;

try
{
    var connectionString = RequiredEnvironmentVariable("SGE_BOOTSTRAP_SQLSERVER");
    var password = ReadSecretFile(RequiredEnvironmentVariable("SGE_BOOTSTRAP_PASSWORD_FILE"));
    var admissionDate = ParseDate(RequiredEnvironmentVariable("SGE_BOOTSTRAP_ADMISSION_DATE"));

    var request = new InitialAdminBootstrapRequest(
        RequiredEnvironmentVariable("SGE_BOOTSTRAP_ORGANIZATION_NAME"),
        RequiredEnvironmentVariable("SGE_BOOTSTRAP_HOSPITAL_UNIT_NAME"),
        RequiredEnvironmentVariable("SGE_BOOTSTRAP_PROFESSION_NAME"),
        RequiredEnvironmentVariable("SGE_BOOTSTRAP_POSITION_NAME"),
        RequiredEnvironmentVariable("SGE_BOOTSTRAP_PROFESSIONAL_LEVEL_CODE"),
        RequiredEnvironmentVariable("SGE_BOOTSTRAP_ADMINISTRATOR_NAME"),
        RequiredEnvironmentVariable("SGE_BOOTSTRAP_ADMINISTRATOR_EMAIL"),
        Environment.GetEnvironmentVariable("SGE_BOOTSTRAP_ADMINISTRATOR_PHONE"),
        admissionDate,
        password);

    var options = new DbContextOptionsBuilder<AppDbContext>()
        .UseSqlServer(connectionString, sql =>
        {
            sql.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName);
            sql.EnableRetryOnFailure(5, TimeSpan.FromSeconds(5), null);
            sql.CommandTimeout(30);
        })
        .Options;
    await using var dbContext = new AppDbContext(options, TimeProvider.System);
    var service = new InitialAdminBootstrapService(
        dbContext,
        new CredentialHasher(),
        new InitialAdminBootstrapRequestValidator(),
        TimeProvider.System);
    var result = await service.ExecuteAsync(request, CancellationToken.None);

    Console.WriteLine("Bootstrap administrativo concluído com sucesso.");
    Console.WriteLine($"OrganizationGuid={result.OrganizationGuid:D}");
    Console.WriteLine($"HospitalUnitGuid={result.HospitalUnitGuid:D}");
    Console.WriteLine($"EmployeeGuid={result.EmployeeGuid:D}");
    Console.WriteLine($"UserGuid={result.UserGuid:D}");
    Console.WriteLine($"ProfileGuid={result.ProfileGuid:D}");
    Console.WriteLine($"GrantedPermissionCount={result.GrantedPermissionCount}");
    return 0;
}
catch (InitialAdminAlreadyProvisionedException exception)
{
    Console.Error.WriteLine(exception.Message);
    return alreadyProvisionedExitCode;
}
catch (Exception exception) when (exception is BootstrapConfigurationException
                                  or ValidationException
                                  or FormatException
                                  or FileNotFoundException
                                  or UnauthorizedAccessException)
{
    Console.Error.WriteLine($"Configuração inválida: {exception.Message}");
    return configurationErrorExitCode;
}
catch (Exception exception)
{
    Console.Error.WriteLine($"Bootstrap não concluído: {exception.Message}");
    return 1;
}

static string RequiredEnvironmentVariable(string name)
{
    var value = Environment.GetEnvironmentVariable(name);
    return !string.IsNullOrWhiteSpace(value)
        ? value
        : throw new BootstrapConfigurationException($"A variável {name} é obrigatória.");
}

static string ReadSecretFile(string path)
{
    var file = new FileInfo(Path.GetFullPath(path));
    if (!file.Exists)
    {
        throw new FileNotFoundException("O arquivo de senha informado não existe.", file.FullName);
    }

    if (file.Length is <= 0 or > 4096)
    {
        throw new BootstrapConfigurationException("O arquivo de senha deve possuir entre 1 e 4096 bytes.");
    }

    return File.ReadAllText(file.FullName).TrimEnd('\r', '\n');
}

static DateOnly ParseDate(string value) =>
    DateOnly.ParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture);

file sealed class BootstrapConfigurationException(string message) : Exception(message);
