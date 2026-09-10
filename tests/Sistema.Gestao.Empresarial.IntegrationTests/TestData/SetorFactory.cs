using Sistema.Gestao.Empresarial.Domain.Organizacoes;
using Sistema.Gestao.Empresarial.Infrastructure.Persistence;

namespace Sistema.Gestao.Empresarial.IntegrationTests.TestData;

/// <summary>
/// Helpers para semear <see cref="CategoriaSetor"/> e <see cref="Setor"/> nos
/// bancos InMemory das suítes (o seed de <c>HasData</c> não é materializado sem
/// <c>EnsureCreated</c>). Mantém as fixtures existentes concisas após o setor
/// hospitalar ganhar categoria obrigatória e sigla.
/// </summary>
internal static class SetorFactory
{
    public static async Task<CategoriaSetor> SeedCategoriaAsync(
        AppDbContext db,
        DateTimeOffset now,
        string nome = "Assistencial")
    {
        var categoria = new CategoriaSetor(Guid.NewGuid(), nome, null, now);
        db.CategoriasSetores.Add(categoria);
        await db.SaveChangesAsync();
        return categoria;
    }

    public static Setor Basico(
        long unidadeHospitalarId,
        long categoriaSetorId,
        string nome,
        DateTimeOffset now,
        string? sigla = null,
        bool permiteAtuacaoCompartilhada = false) =>
        new(
            Guid.NewGuid(),
            unidadeHospitalarId,
            categoriaSetorId,
            nome,
            sigla ?? DerivarSigla(nome),
            descricao: null,
            localizacaoInterna: null,
            ramal: null,
            email: null,
            responsavelFuncionarioId: null,
            assistencial: false,
            permiteAlocacaoEscala: false,
            permiteAtuacaoCompartilhada: permiteAtuacaoCompartilhada,
            now);

    private static string DerivarSigla(string nome)
    {
        var iniciais = new string(nome
            .Split([' '], StringSplitOptions.RemoveEmptyEntries)
            .Select(parte => parte[0])
            .ToArray())
            .ToUpperInvariant();
        return string.IsNullOrEmpty(iniciais) ? "SET" : iniciais[..Math.Min(20, iniciais.Length)];
    }
}
