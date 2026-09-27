using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Sistema.Gestao.Empresarial.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Níveis profissionais passam a ser um catálogo configurável (issue #47):
    /// código e nome com collation <c>Latin1_General_CI_AS</c> e índices únicos
    /// filtrados, permissões de escrita, e o fim do seed obrigatório JR/PL/SR.
    /// </summary>
    /// <remarks>
    /// O scaffolding do EF gerou <c>DeleteData</c> físico dos níveis 1–3 (removidos do
    /// <c>HasData</c>) e o <c>InsertData</c> correspondente no <c>Down</c>. Ambos foram
    /// substituídos: exclusão física quebraria bancos com funcionários vinculados (FK
    /// <c>Restrict</c>) e violaria a regra de nunca remover registros de negócio. Ver
    /// <see cref="LegacyProfessionalLevelSeed"/> para a estratégia de compatibilidade.
    /// </remarks>
    public partial class NiveisProfissionaisConfiguraveis : Migration
    {
        private const string Collation = "Latin1_General_CI_AS";

        private static readonly DateTimeOffset SeedDate =
            new(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), TimeSpan.Zero);

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Trava de segurança (mesmo padrão de BusinessKeyCaseInsensitiveCollation):
            // se o banco usar collation sensível a caixa e já tiver níveis ativos que
            // passam a colidir sob Latin1_General_CI_AS, aborta antes de qualquer
            // alteração, apontando o que sanear. Nenhum dado é apagado.
            migrationBuilder.Sql($@"
DECLARE @duplicados nvarchar(max);
DECLARE @mensagem nvarchar(2048);

SELECT @duplicados = STRING_AGG(CONVERT(nvarchar(max), Chave), N', ')
FROM (
    SELECT CONCAT(N'código ', QUOTENAME([Codigo] COLLATE {Collation})) AS Chave
    FROM [sge].[NiveisProfissionais]
    WHERE [Excluido] = 0
    GROUP BY [Codigo] COLLATE {Collation}
    HAVING COUNT(*) > 1
    UNION ALL
    SELECT CONCAT(N'nome ', QUOTENAME([Nome] COLLATE {Collation}))
    FROM [sge].[NiveisProfissionais]
    WHERE [Excluido] = 0
    GROUP BY [Nome] COLLATE {Collation}
    HAVING COUNT(*) > 1
) AS d;

IF @duplicados IS NOT NULL
BEGIN
    SET @mensagem = CONCAT(
        N'Existem níveis profissionais ativos que se tornam duplicados sob a collation {Collation}. ',
        N'Saneie os registros antes de aplicar a migration: ', @duplicados);
    THROW 50001, @mensagem, 1;
END;
");

            // O índice único depende de [Codigo]; é derrubado para permitir a troca de
            // collation e recriado em seguida herdando-a.
            migrationBuilder.DropIndex(
                name: "IX_NiveisProfissionais_Codigo",
                schema: "sge",
                table: "NiveisProfissionais");

            migrationBuilder.AlterColumn<string>(
                name: "Nome",
                schema: "sge",
                table: "NiveisProfissionais",
                type: "nvarchar(80)",
                maxLength: 80,
                nullable: false,
                collation: Collation,
                oldClrType: typeof(string),
                oldType: "nvarchar(80)",
                oldMaxLength: 80);

            migrationBuilder.AlterColumn<string>(
                name: "Codigo",
                schema: "sge",
                table: "NiveisProfissionais",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: false,
                collation: Collation,
                oldClrType: typeof(string),
                oldType: "nvarchar(10)",
                oldMaxLength: 10);

            migrationBuilder.CreateIndex(
                name: "IX_NiveisProfissionais_Codigo",
                schema: "sge",
                table: "NiveisProfissionais",
                column: "Codigo",
                unique: true,
                filter: "[Excluido] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_NiveisProfissionais_Nome",
                schema: "sge",
                table: "NiveisProfissionais",
                column: "Nome",
                unique: true,
                filter: "[Excluido] = 0");

            migrationBuilder.InsertData(
                schema: "sge",
                table: "Permissoes",
                columns: new[] { "Id", "Ativo", "Codigo", "DataAtualizacao", "DataCriacao", "Descricao", "Excluido", "ExcluidoEm", "ExcluidoPor", "Guid" },
                values: new object[,]
                {
                    { 18L, true, "NIVEL_PROFISSIONAL_CRIAR", SeedDate, SeedDate, "Criar níveis profissionais", false, null, null, new Guid("5c0f2e61-7b3d-4e0a-9f4b-2d8e6a1c9b37") },
                    { 19L, true, "NIVEL_PROFISSIONAL_EDITAR", SeedDate, SeedDate, "Editar e excluir níveis profissionais", false, null, null, new Guid("a7e4b9d2-3c61-4f58-8e0d-6b2f1a9c4e73") }
                });

            // Fim do seed obrigatório JR/PL/SR, sem remoção física: preservados em
            // bancos já em uso, excluídos logicamente em instalação nova.
            migrationBuilder.Sql(LegacyProfessionalLevelSeed.RetireOnNewInstallationSql);

            // Provisionamento de upgrade: o ADMINISTRADOR_INICIAL já existente recebe
            // as novas permissões (no-op em instalação nova, onde o bootstrap concede
            // todo o catálogo).
            migrationBuilder.Sql(
                AdministratorProfilePermissionBackfill.GrantProfessionalLevelPermissionsToInitialAdministratorSql);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Remove as associações antes que a FK Restrict bloqueie a exclusão das
            // permissões do catálogo.
            migrationBuilder.Sql(AdministratorProfilePermissionBackfill.RevokeProfessionalLevelPermissionsSql);

            migrationBuilder.DeleteData(schema: "sge", table: "Permissoes", keyColumn: "Id", keyValue: 18L);
            migrationBuilder.DeleteData(schema: "sge", table: "Permissoes", keyColumn: "Id", keyValue: 19L);

            // Os níveis de exemplo continuam existindo fisicamente; a reversão apenas
            // desfaz a exclusão lógica feita pelo Up (nada é reinserido).
            migrationBuilder.Sql(LegacyProfessionalLevelSeed.RestoreRetiredSql);

            migrationBuilder.DropIndex(
                name: "IX_NiveisProfissionais_Nome",
                schema: "sge",
                table: "NiveisProfissionais");

            migrationBuilder.DropIndex(
                name: "IX_NiveisProfissionais_Codigo",
                schema: "sge",
                table: "NiveisProfissionais");

            migrationBuilder.AlterColumn<string>(
                name: "Nome",
                schema: "sge",
                table: "NiveisProfissionais",
                type: "nvarchar(80)",
                maxLength: 80,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(80)",
                oldMaxLength: 80,
                oldCollation: Collation);

            migrationBuilder.AlterColumn<string>(
                name: "Codigo",
                schema: "sge",
                table: "NiveisProfissionais",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(10)",
                oldMaxLength: 10,
                oldCollation: Collation);

            migrationBuilder.CreateIndex(
                name: "IX_NiveisProfissionais_Codigo",
                schema: "sge",
                table: "NiveisProfissionais",
                column: "Codigo",
                unique: true,
                filter: "[Excluido] = 0");
        }
    }
}
