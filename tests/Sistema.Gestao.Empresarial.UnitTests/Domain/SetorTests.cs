using Sistema.Gestao.Empresarial.Domain.Common;
using Sistema.Gestao.Empresarial.Domain.Organizacoes;

namespace Sistema.Gestao.Empresarial.UnitTests.Domain;

public sealed class SetorTests
{
    private static readonly DateTimeOffset CriadoEm =
        new(2026, 9, 10, 12, 0, 0, TimeSpan.Zero);

    private static Setor NovoSetor(
        string nome = "Central de Análise de Prescrições",
        string sigla = "cap",
        long categoriaSetorId = 5,
        long? responsavelFuncionarioId = null,
        bool permiteAtuacaoCompartilhada = false) =>
        new(
            Guid.NewGuid(),
            unidadeHospitalarId: 1,
            categoriaSetorId,
            nome,
            sigla,
            descricao: null,
            localizacaoInterna: null,
            ramal: null,
            email: null,
            responsavelFuncionarioId,
            assistencial: true,
            permiteAlocacaoEscala: true,
            permiteAtuacaoCompartilhada,
            CriadoEm);

    [Fact]
    public void Criar_DeveNormalizarSiglaParaMaiusculasESemEspacos()
    {
        var setor = NovoSetor(sigla: "  cap ");

        Assert.Equal("CAP", setor.Sigla);
        Assert.Equal("Central de Análise de Prescrições", setor.Nome);
    }

    [Fact]
    public void Criar_DeveNormalizarEmailParaMinusculasEExigirCategoriaEUnidade()
    {
        var setor = new Setor(
            Guid.NewGuid(), 1, 5, "Beira Leito", "BL",
            "  Suporte assistencial  ", " Bloco A ", " 2100 ", "  SETOR@Hospital.Test ",
            null, false, false, false, CriadoEm);

        Assert.Equal("Suporte assistencial", setor.Descricao);
        Assert.Equal("Bloco A", setor.LocalizacaoInterna);
        Assert.Equal("2100", setor.Ramal);
        Assert.Equal("setor@hospital.test", setor.Email);

        Assert.Throws<DomainException>(() => new Setor(
            Guid.NewGuid(), 0, 5, "X", "X", null, null, null, null, null,
            false, false, false, CriadoEm));
        Assert.Throws<DomainException>(() => new Setor(
            Guid.NewGuid(), 1, 0, "X", "X", null, null, null, null, null,
            false, false, false, CriadoEm));
    }

    [Fact]
    public void Criar_SemNomeOuSigla_DeveFalhar()
    {
        Assert.Throws<DomainException>(() => NovoSetor(nome: "   "));
        Assert.Throws<DomainException>(() => NovoSetor(sigla: "   "));
    }

    [Fact]
    public void Atualizar_DeveNormalizarValoresESerIdempotente()
    {
        var setor = NovoSetor();
        var atualizadoEm = CriadoEm.AddMinutes(10);

        var mudou = setor.Atualizar(
            categoriaSetorId: 7,
            nome: "  Central de Prescrições  ",
            sigla: " cap ",
            descricao: "  Nova descrição  ",
            localizacaoInterna: null,
            ramal: null,
            email: null,
            responsavelFuncionarioId: 42,
            assistencial: true,
            permiteAlocacaoEscala: true,
            permiteAtuacaoCompartilhada: true,
            atualizadoEm);

        Assert.True(mudou);
        Assert.Equal(7, setor.CategoriaSetorId);
        Assert.Equal("Central de Prescrições", setor.Nome);
        Assert.Equal("CAP", setor.Sigla);
        Assert.Equal("Nova descrição", setor.Descricao);
        Assert.Equal(42, setor.ResponsavelFuncionarioId);
        Assert.True(setor.PermiteAtuacaoCompartilhada);
        Assert.Equal(atualizadoEm, setor.DataAtualizacao);

        var mudouDeNovo = setor.Atualizar(
            7, "Central de Prescrições", "CAP", "Nova descrição", null, null, null,
            42, true, true, true, atualizadoEm.AddMinutes(5));

        Assert.False(mudouDeNovo);
        Assert.Equal(atualizadoEm, setor.DataAtualizacao);
    }

    [Fact]
    public void Atualizar_ComResponsavelInvalido_DeveFalhar()
    {
        var setor = NovoSetor();

        Assert.Throws<DomainException>(() => setor.Atualizar(
            5, "Nome", "SIG", null, null, null, null,
            responsavelFuncionarioId: 0,
            assistencial: false, permiteAlocacaoEscala: false, permiteAtuacaoCompartilhada: false,
            CriadoEm.AddMinutes(1)));
    }

    [Fact]
    public void InativarEReativar_DeveAlternarStatusSemExcluir()
    {
        var setor = NovoSetor();

        setor.Inativar(CriadoEm.AddDays(1));
        Assert.False(setor.Ativo);
        Assert.False(setor.Excluido);

        setor.Reativar(CriadoEm.AddDays(2));
        Assert.True(setor.Ativo);
    }
}

public sealed class CategoriaSetorTests
{
    private static readonly DateTimeOffset CriadoEm =
        new(2026, 9, 10, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Atualizar_DeveNormalizarValoresESerIdempotente()
    {
        var categoria = new CategoriaSetor(Guid.NewGuid(), "Assistencial", null, CriadoEm);
        var atualizadoEm = CriadoEm.AddMinutes(1);

        Assert.True(categoria.Atualizar("  Apoio Assistencial  ", "  Suporte  ", atualizadoEm));
        Assert.Equal("Apoio Assistencial", categoria.Nome);
        Assert.Equal("Suporte", categoria.Descricao);
        Assert.Equal(atualizadoEm, categoria.DataAtualizacao);

        Assert.False(categoria.Atualizar("Apoio Assistencial", "Suporte", atualizadoEm.AddMinutes(1)));
        Assert.Equal(atualizadoEm, categoria.DataAtualizacao);
    }

    [Fact]
    public void Criar_SemNome_DeveFalhar() =>
        Assert.Throws<DomainException>(() => new CategoriaSetor(Guid.NewGuid(), "  ", null, CriadoEm));
}

public sealed class SetorUnidadeAtendidaTests
{
    private static readonly DateTimeOffset CriadoEm =
        new(2026, 9, 10, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Encerrar_ComDataAnteriorAoInicio_DeveFalhar()
    {
        var vinculo = new SetorUnidadeAtendida(
            Guid.NewGuid(), 1, 2, new DateOnly(2026, 3, 1), CriadoEm);

        Assert.Throws<DomainException>(() =>
            vinculo.Encerrar(new DateOnly(2026, 2, 1), CriadoEm.AddDays(1)));
    }

    [Fact]
    public void Encerrar_DeveInativarERegistrarDataFim_EhIdempotente()
    {
        var vinculo = new SetorUnidadeAtendida(
            Guid.NewGuid(), 1, 2, new DateOnly(2026, 3, 1), CriadoEm);

        Assert.True(vinculo.Encerrar(new DateOnly(2026, 4, 1), CriadoEm.AddDays(1)));
        Assert.False(vinculo.Ativo);
        Assert.Equal(new DateOnly(2026, 4, 1), vinculo.DataFim);

        Assert.False(vinculo.Encerrar(new DateOnly(2026, 5, 1), CriadoEm.AddDays(2)));
        Assert.Equal(new DateOnly(2026, 4, 1), vinculo.DataFim);
    }
}
