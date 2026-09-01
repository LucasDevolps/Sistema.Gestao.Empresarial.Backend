using System.Text.RegularExpressions;

namespace Sistema.Gestao.Empresarial.IntegrationTests.Architecture;

public sealed partial class NoPhysicalDeleteTests
{
    [Fact]
    public void CodigoDeProducao_NaoDeveConterApisDeExclusaoFisica()
    {
        var root = FindRepositoryRoot();
        var sourceFiles = Directory.EnumerateFiles(Path.Combine(root, "src"), "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}"));

        var violations = sourceFiles
            .SelectMany(path => ForbiddenDeletePattern().Matches(File.ReadAllText(path))
                .Select(match => $"{Path.GetRelativePath(root, path)}: {match.Value}"))
            .ToArray();

        Assert.Empty(violations);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Sistema.Gestao.Empresarial.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
            ?? throw new DirectoryNotFoundException("Raiz da solution não encontrada.");
    }

    [GeneratedRegex(@"\.(Remove|RemoveRange|ExecuteDelete|ExecuteDeleteAsync)\s*\(", RegexOptions.CultureInvariant)]
    private static partial Regex ForbiddenDeletePattern();
}
