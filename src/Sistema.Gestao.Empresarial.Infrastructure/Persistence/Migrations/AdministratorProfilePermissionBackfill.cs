namespace Sistema.Gestao.Empresarial.Infrastructure.Persistence.Migrations;

/// <summary>
/// SQL idempotente que concede ao perfil administrativo inicial
/// (<c>ADMINISTRADOR_INICIAL</c>), quando ele já existe, as permissões
/// introduzidas por uma feature posterior.
/// </summary>
/// <remarks>
/// O bootstrap inicial associa ao perfil apenas as permissões existentes no
/// momento da criação do administrador e não é reexecutado após uma atualização de
/// banco. Como a autorização impede que um usuário conceda uma permissão que ele
/// próprio não possui, sem este passo as novas telas ficariam inacessíveis em uma
/// instalação já provisionada. Instalações novas continuam recebendo tudo pelo
/// bootstrap — nestas o <c>INSERT</c> abaixo não encontra o perfil e é um no-op.
///
/// O SQL é reutilizado pela migration e pelo teste de RealInfrastructure que valida
/// a idempotência; não enfraquece a autorização (grava linhas reais em
/// <c>PerfisPermissoes</c>, que continua sendo a fonte de verdade) e respeita o
/// índice único filtrado <c>(PerfilId, PermissaoId) WHERE [Excluido] = 0</c>.
/// </remarks>
internal static class AdministratorProfilePermissionBackfill
{
    public const string InitialAdministratorProfileName = "ADMINISTRADOR_INICIAL";

    /// <summary>Códigos introduzidos pela migration <c>GestaoSetoresHospitalares</c>.</summary>
    public static readonly IReadOnlyList<string> SectorPermissionCodes =
    [
        "SETOR_CRIAR",
        "CATEGORIA_SETOR_VISUALIZAR",
        "CATEGORIA_SETOR_CRIAR",
        "CATEGORIA_SETOR_EDITAR",
    ];

    /// <summary>
    /// Associa as permissões de setor ao perfil administrativo inicial existente.
    /// Idempotente: o <c>NOT EXISTS</c> impede duplicidade em reexecuções; não
    /// remove nem altera associações existentes e não toca em outros perfis.
    /// </summary>
    public const string GrantSectorPermissionsToInitialAdministratorSql = @"
DECLARE @provisionedAt datetimeoffset(0) = '2026-01-01T00:00:00+00:00';

INSERT INTO [sge].[PerfisPermissoes]
    ([Guid], [PerfilId], [PermissaoId], [Ativo], [Excluido], [DataCriacao], [DataAtualizacao])
SELECT NEWID(), pf.[Id], pm.[Id], 1, 0, @provisionedAt, @provisionedAt
FROM [sge].[Perfis] AS pf
CROSS JOIN [sge].[Permissoes] AS pm
WHERE pf.[Nome] = N'ADMINISTRADOR_INICIAL'
  AND pf.[Excluido] = 0
  AND pm.[Excluido] = 0
  AND pm.[Codigo] IN (
      N'SETOR_CRIAR', N'CATEGORIA_SETOR_VISUALIZAR', N'CATEGORIA_SETOR_CRIAR', N'CATEGORIA_SETOR_EDITAR')
  AND NOT EXISTS (
      SELECT 1
      FROM [sge].[PerfisPermissoes] AS existing
      WHERE existing.[PerfilId] = pf.[Id]
        AND existing.[PermissaoId] = pm.[Id]
        AND existing.[Excluido] = 0);
";

    /// <summary>
    /// Reversão: remove as associações criadas acima. Necessária no <c>Down</c>
    /// porque a FK <c>PerfisPermissoes → Permissoes</c> é <c>Restrict</c> e a
    /// própria migration remove as permissões do catálogo.
    /// </summary>
    public const string RevokeSectorPermissionsFromInitialAdministratorSql = @"
DELETE existing
FROM [sge].[PerfisPermissoes] AS existing
INNER JOIN [sge].[Perfis] AS pf ON pf.[Id] = existing.[PerfilId]
INNER JOIN [sge].[Permissoes] AS pm ON pm.[Id] = existing.[PermissaoId]
WHERE pf.[Nome] = N'ADMINISTRADOR_INICIAL'
  AND pm.[Codigo] IN (
      N'SETOR_CRIAR', N'CATEGORIA_SETOR_VISUALIZAR', N'CATEGORIA_SETOR_CRIAR', N'CATEGORIA_SETOR_EDITAR');
";
}
