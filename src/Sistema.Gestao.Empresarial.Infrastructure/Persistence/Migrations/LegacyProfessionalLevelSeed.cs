namespace Sistema.Gestao.Empresarial.Infrastructure.Persistence.Migrations;

/// <summary>
/// Tratamento dos níveis profissionais <c>JR</c>/<c>PL</c>/<c>SR</c> que a migration
/// <c>InitialOrganizationalFoundation</c> inseria como dados estruturais. A partir da
/// migration <c>NiveisProfissionaisConfiguraveis</c> o catálogo pertence ao gestor e
/// nenhum nível é semeado.
/// </summary>
/// <remarks>
/// A migration histórica não é editada (ela já foi aplicada em ambientes
/// existentes). A compatibilidade é resolvida aqui, sem nenhuma remoção física:
/// <list type="bullet">
/// <item><b>Banco já em uso</b> (existe ao menos um funcionário, portanto o bootstrap
/// já rodou): os três registros permanecem intactos e passam a ser dados comuns do
/// catálogo — editáveis e, quando não vinculados a funcionários, excluíveis pela
/// aplicação. Nenhum vínculo, histórico ou tela existente muda.</item>
/// <item><b>Instalação nova</b> (nenhum funcionário): os três registros de exemplo
/// são excluídos logicamente, de modo que o catálogo começa vazio. Como os índices
/// únicos são filtrados por <c>[Excluido] = 0</c>, o gestor pode recriar os mesmos
/// códigos/nomes. <c>ExcluidoPor</c> fica <c>NULL</c>: marca a exclusão feita pela
/// migration (exclusões da aplicação sempre registram o usuário responsável).</item>
/// </list>
/// Os registros são identificados por <c>Id</c> <b>e</b> <c>Guid</c> semeados, nunca
/// por código ou nome, para não atingir níveis cadastrados pelo gestor.
/// </remarks>
internal static class LegacyProfessionalLevelSeed
{
    private const string LegacySeedRowsPredicate = @"(
       (n.[Id] = 1 AND n.[Guid] = '870D89D7-153A-46EB-93E4-A2E08E966D19')
    OR (n.[Id] = 2 AND n.[Guid] = '9D77BFD3-DC47-44E5-A4CA-B62497CD5864')
    OR (n.[Id] = 3 AND n.[Guid] = 'E6E15AE5-FF9B-4A07-884A-5E66F805BFE0'))";

    /// <summary>
    /// Exclui logicamente os níveis de exemplo somente em instalação nova. Idempotente.
    /// </summary>
    public const string RetireOnNewInstallationSql = @"
IF NOT EXISTS (SELECT 1 FROM [sge].[Funcionarios])
BEGIN
    DECLARE @now datetimeoffset(0) = TODATETIMEOFFSET(SYSUTCDATETIME(), 0);

    UPDATE n
    SET [Excluido] = 1,
        [Ativo] = 0,
        [ExcluidoEm] = @now,
        [ExcluidoPor] = NULL,
        [DataAtualizacao] = @now
    FROM [sge].[NiveisProfissionais] AS n
    WHERE n.[Excluido] = 0
      AND " + LegacySeedRowsPredicate + @";
END;
";

    /// <summary>
    /// Reversão: reativa apenas os níveis de exemplo que esta migration excluiu
    /// (<c>ExcluidoPor IS NULL</c>) e somente quando nenhum nível ativo já usa o mesmo
    /// código ou nome — o que violaria os índices únicos filtrados.
    /// </summary>
    public const string RestoreRetiredSql = @"
DECLARE @now datetimeoffset(0) = TODATETIMEOFFSET(SYSUTCDATETIME(), 0);

UPDATE n
SET [Excluido] = 0,
    [Ativo] = 1,
    [ExcluidoEm] = NULL,
    [ExcluidoPor] = NULL,
    [DataAtualizacao] = @now
FROM [sge].[NiveisProfissionais] AS n
WHERE n.[Excluido] = 1
  AND n.[ExcluidoPor] IS NULL
  AND " + LegacySeedRowsPredicate + @"
  AND NOT EXISTS (
      SELECT 1
      FROM [sge].[NiveisProfissionais] AS other
      WHERE other.[Excluido] = 0
        AND other.[Id] <> n.[Id]
        AND (other.[Codigo] = n.[Codigo] OR other.[Nome] = n.[Nome]));
";
}
