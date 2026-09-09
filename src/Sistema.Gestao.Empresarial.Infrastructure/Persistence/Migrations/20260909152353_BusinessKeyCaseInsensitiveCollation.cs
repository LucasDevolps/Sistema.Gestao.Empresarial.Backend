using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sistema.Gestao.Empresarial.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class BusinessKeyCaseInsensitiveCollation : Migration
    {
        private const string Collation = "Latin1_General_CI_AS";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Trava de segurança (item 18): se o banco de destino usar uma collation
            // sensível a caixa e já contiver títulos/nomes que passam a colidir sob
            // Latin1_General_CI_AS, a recriação do índice único falharia com um erro
            // obscuro. Aqui a migration aborta antes de qualquer alteração, com uma
            // mensagem que aponta exatamente o que precisa ser saneado. Nenhum dado é
            // apagado automaticamente.
            migrationBuilder.Sql($@"
DECLARE @duplicados nvarchar(max);
DECLARE @mensagem nvarchar(2048);

SELECT @duplicados = STRING_AGG(CONVERT(nvarchar(max), QUOTENAME(NomeChave)), N', ')
FROM (
    SELECT Nome COLLATE {Collation} AS NomeChave
    FROM [sge].[Profissoes]
    WHERE [Excluido] = 0
    GROUP BY Nome COLLATE {Collation}
    HAVING COUNT(*) > 1
) AS d;

IF @duplicados IS NOT NULL
BEGIN
    SET @mensagem = CONCAT(
        N'Existem profissoes ativas que se tornam duplicadas sob a collation {Collation}. ',
        N'Saneie os registros antes de aplicar a migration: ', @duplicados);
    THROW 50001, @mensagem, 1;
END;

SELECT @duplicados = STRING_AGG(CONVERT(nvarchar(max), QUOTENAME(NomeChave)), N', ')
FROM (
    SELECT Nome COLLATE {Collation} AS NomeChave
    FROM [sge].[Cargos]
    WHERE [Excluido] = 0
    GROUP BY Nome COLLATE {Collation}
    HAVING COUNT(*) > 1
) AS d;

IF @duplicados IS NOT NULL
BEGIN
    SET @mensagem = CONCAT(
        N'Existem cargos ativos que se tornam duplicados sob a collation {Collation}. ',
        N'Saneie os registros antes de aplicar a migration: ', @duplicados);
    THROW 50001, @mensagem, 1;
END;
");

            // SQL Server não permite alterar a collation de uma coluna enquanto houver
            // índice dependente dela; então o índice único filtrado é derrubado,
            // a coluna recebe a collation explícita e o índice é recriado (herdando-a).
            migrationBuilder.DropIndex(name: "IX_Profissoes_Nome", schema: "sge", table: "Profissoes");
            migrationBuilder.DropIndex(name: "IX_Cargos_Nome", schema: "sge", table: "Cargos");

            migrationBuilder.AlterColumn<string>(
                name: "Nome",
                schema: "sge",
                table: "Profissoes",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: false,
                collation: Collation,
                oldClrType: typeof(string),
                oldType: "nvarchar(150)",
                oldMaxLength: 150);

            migrationBuilder.AlterColumn<string>(
                name: "Nome",
                schema: "sge",
                table: "Cargos",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: false,
                collation: Collation,
                oldClrType: typeof(string),
                oldType: "nvarchar(150)",
                oldMaxLength: 150);

            migrationBuilder.CreateIndex(
                name: "IX_Profissoes_Nome",
                schema: "sge",
                table: "Profissoes",
                column: "Nome",
                unique: true,
                filter: "[Excluido] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_Cargos_Nome",
                schema: "sge",
                table: "Cargos",
                column: "Nome",
                unique: true,
                filter: "[Excluido] = 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(name: "IX_Profissoes_Nome", schema: "sge", table: "Profissoes");
            migrationBuilder.DropIndex(name: "IX_Cargos_Nome", schema: "sge", table: "Cargos");

            migrationBuilder.AlterColumn<string>(
                name: "Nome",
                schema: "sge",
                table: "Profissoes",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(150)",
                oldMaxLength: 150,
                oldCollation: Collation);

            migrationBuilder.AlterColumn<string>(
                name: "Nome",
                schema: "sge",
                table: "Cargos",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(150)",
                oldMaxLength: 150,
                oldCollation: Collation);

            migrationBuilder.CreateIndex(
                name: "IX_Profissoes_Nome",
                schema: "sge",
                table: "Profissoes",
                column: "Nome",
                unique: true,
                filter: "[Excluido] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_Cargos_Nome",
                schema: "sge",
                table: "Cargos",
                column: "Nome",
                unique: true,
                filter: "[Excluido] = 0");
        }
    }
}
