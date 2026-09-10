using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Sistema.Gestao.Empresarial.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class GestaoSetoresHospitalares : Migration
    {
        private const string Collation = "Latin1_General_CI_AS";

        private static readonly DateTimeOffset SeedDate =
            new(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), TimeSpan.Zero);

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Trava de segurança (mesmo padrão de BusinessKeyCaseInsensitiveCollation): se o
            // banco de destino usar collation sensível a caixa e já tiver setores que passam
            // a colidir sob Latin1_General_CI_AS dentro da mesma unidade, aborta antes de
            // qualquer alteração, apontando o que sanear. Nenhum dado é apagado.
            migrationBuilder.Sql($@"
DECLARE @duplicados nvarchar(max);
DECLARE @mensagem nvarchar(2048);

SELECT @duplicados = STRING_AGG(CONVERT(nvarchar(max), QUOTENAME(NomeChave)), N', ')
FROM (
    SELECT CONCAT(UnidadeHospitalarId, N' / ', Nome COLLATE {Collation}) AS NomeChave
    FROM [sge].[Setores]
    WHERE [Excluido] = 0
    GROUP BY UnidadeHospitalarId, Nome COLLATE {Collation}
    HAVING COUNT(*) > 1
) AS d;

IF @duplicados IS NOT NULL
BEGIN
    SET @mensagem = CONCAT(
        N'Existem setores ativos que se tornam duplicados sob a collation {Collation}. ',
        N'Saneie os registros antes de aplicar a migration: ', @duplicados);
    THROW 50001, @mensagem, 1;
END;
");

            // O índice único depende de [Nome]; é derrubado para permitir a troca de collation
            // e recriado ao final herdando-a.
            migrationBuilder.DropIndex(
                name: "IX_Setores_UnidadeHospitalarId_Nome",
                schema: "sge",
                table: "Setores");

            migrationBuilder.AlterColumn<string>(
                name: "Nome",
                schema: "sge",
                table: "Setores",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: false,
                collation: Collation,
                oldClrType: typeof(string),
                oldType: "nvarchar(150)",
                oldMaxLength: 150);

            migrationBuilder.AddColumn<bool>(
                name: "Assistencial",
                schema: "sge",
                table: "Setores",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Descricao",
                schema: "sge",
                table: "Setores",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Email",
                schema: "sge",
                table: "Setores",
                type: "nvarchar(254)",
                maxLength: 254,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LocalizacaoInterna",
                schema: "sge",
                table: "Setores",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "PermiteAlocacaoEscala",
                schema: "sge",
                table: "Setores",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "PermiteAtuacaoCompartilhada",
                schema: "sge",
                table: "Setores",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Ramal",
                schema: "sge",
                table: "Setores",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "ResponsavelFuncionarioId",
                schema: "sge",
                table: "Setores",
                type: "bigint",
                nullable: true);

            // Colunas obrigatórias no modelo, adicionadas como anuláveis para permitir o
            // backfill de eventuais linhas pré-existentes antes de aplicar NOT NULL / FK.
            migrationBuilder.AddColumn<long>(
                name: "CategoriaSetorId",
                schema: "sge",
                table: "Setores",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Sigla",
                schema: "sge",
                table: "Setores",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true,
                collation: Collation);

            migrationBuilder.CreateTable(
                name: "CategoriasSetores",
                schema: "sge",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Nome = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false, collation: "Latin1_General_CI_AS"),
                    Descricao = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Guid = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Ativo = table.Column<bool>(type: "bit", nullable: false),
                    Excluido = table.Column<bool>(type: "bit", nullable: false),
                    DataCriacao = table.Column<DateTimeOffset>(type: "datetimeoffset(0)", precision: 0, nullable: false),
                    DataAtualizacao = table.Column<DateTimeOffset>(type: "datetimeoffset(0)", precision: 0, nullable: false),
                    ExcluidoEm = table.Column<DateTimeOffset>(type: "datetimeoffset(0)", precision: 0, nullable: true),
                    ExcluidoPor = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Versao = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CategoriasSetores", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SetoresUnidadesAtendidas",
                schema: "sge",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SetorId = table.Column<long>(type: "bigint", nullable: false),
                    UnidadeHospitalarId = table.Column<long>(type: "bigint", nullable: false),
                    DataInicio = table.Column<DateOnly>(type: "date", nullable: false),
                    DataFim = table.Column<DateOnly>(type: "date", nullable: true),
                    Guid = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Ativo = table.Column<bool>(type: "bit", nullable: false),
                    Excluido = table.Column<bool>(type: "bit", nullable: false),
                    DataCriacao = table.Column<DateTimeOffset>(type: "datetimeoffset(0)", precision: 0, nullable: false),
                    DataAtualizacao = table.Column<DateTimeOffset>(type: "datetimeoffset(0)", precision: 0, nullable: false),
                    ExcluidoEm = table.Column<DateTimeOffset>(type: "datetimeoffset(0)", precision: 0, nullable: true),
                    ExcluidoPor = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Versao = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SetoresUnidadesAtendidas", x => x.Id);
                    table.CheckConstraint("CK_SetorUnidadeAtendida_Periodo", "[DataFim] IS NULL OR [DataFim] >= [DataInicio]");
                    table.ForeignKey(
                        name: "FK_SetoresUnidadesAtendidas_Setores_SetorId",
                        column: x => x.SetorId,
                        principalSchema: "sge",
                        principalTable: "Setores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SetoresUnidadesAtendidas_UnidadesHospitalares_UnidadeHospitalarId",
                        column: x => x.UnidadeHospitalarId,
                        principalSchema: "sge",
                        principalTable: "UnidadesHospitalares",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                schema: "sge",
                table: "CategoriasSetores",
                columns: new[] { "Id", "Ativo", "DataAtualizacao", "DataCriacao", "Descricao", "Excluido", "ExcluidoEm", "ExcluidoPor", "Guid", "Nome" },
                values: new object[,]
                {
                    { 1L, true, SeedDate, SeedDate, null, false, null, null, new Guid("f1b81208-6af0-4aad-8be1-deed557e5537"), "Assistencial" },
                    { 2L, true, SeedDate, SeedDate, null, false, null, null, new Guid("1ca290f5-b8f1-470d-8eb4-fa66972982cf"), "Administrativo" },
                    { 3L, true, SeedDate, SeedDate, null, false, null, null, new Guid("1d3ea693-d7ad-48dc-bf02-7ab702412d22"), "Diagnóstico" },
                    { 4L, true, SeedDate, SeedDate, null, false, null, null, new Guid("4f21abb7-9af4-438e-917a-fb6fb99e5a3f"), "Cirúrgico" },
                    { 5L, true, SeedDate, SeedDate, null, false, null, null, new Guid("16e087c3-a9cf-4f66-9446-81b7cc12fcb1"), "Emergência" },
                    { 6L, true, SeedDate, SeedDate, null, false, null, null, new Guid("f628f4e9-dec6-4aa4-8486-e660b6f21a34"), "Internação" },
                    { 7L, true, SeedDate, SeedDate, null, false, null, null, new Guid("02bb6bba-8bc6-41b1-97a7-a5065803ca9a"), "Apoio Assistencial" },
                    { 8L, true, SeedDate, SeedDate, null, false, null, null, new Guid("2a61a94f-6106-40eb-b6cd-84be7f3633d9"), "Farmacêutico" },
                    { 9L, true, SeedDate, SeedDate, null, false, null, null, new Guid("2db11a0d-a482-4292-a8c7-9d8c94448c75"), "Logístico" },
                    { 10L, true, SeedDate, SeedDate, null, false, null, null, new Guid("12f0bac5-bb41-4c67-93d3-b1edbd3551c2"), "Outro" }
                });

            migrationBuilder.InsertData(
                schema: "sge",
                table: "Permissoes",
                columns: new[] { "Id", "Ativo", "Codigo", "DataAtualizacao", "DataCriacao", "Descricao", "Excluido", "ExcluidoEm", "ExcluidoPor", "Guid" },
                values: new object[,]
                {
                    { 14L, true, "SETOR_CRIAR", SeedDate, SeedDate, "Criar setores", false, null, null, new Guid("3713e2d5-9d19-4bad-aefb-c97e1ac03a2e") },
                    { 15L, true, "CATEGORIA_SETOR_VISUALIZAR", SeedDate, SeedDate, "Visualizar categorias de setor", false, null, null, new Guid("819c7eb3-278f-470e-a9b2-d9dbc3c9ee87") },
                    { 16L, true, "CATEGORIA_SETOR_CRIAR", SeedDate, SeedDate, "Criar categorias de setor", false, null, null, new Guid("72d43bb2-4da1-4ba5-a32b-defccccc69d4") },
                    { 17L, true, "CATEGORIA_SETOR_EDITAR", SeedDate, SeedDate, "Editar categorias de setor", false, null, null, new Guid("8e84d66f-a105-4748-86ff-e069a73f0065") }
                });

            // Backfill de linhas pré-existentes: categoria "Outro" (Id 10) e uma sigla única
            // derivada do Id. Em bancos sem setores cadastrados nada é alterado.
            migrationBuilder.Sql(@"
UPDATE [sge].[Setores] SET [CategoriaSetorId] = 10 WHERE [CategoriaSetorId] IS NULL;
UPDATE [sge].[Setores]
SET [Sigla] = CONCAT('S', RIGHT(REPLICATE('0', 6) + CONVERT(varchar(10), [Id]), 6))
WHERE [Sigla] IS NULL;
");

            migrationBuilder.AlterColumn<long>(
                name: "CategoriaSetorId",
                schema: "sge",
                table: "Setores",
                type: "bigint",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Sigla",
                schema: "sge",
                table: "Setores",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                collation: Collation,
                oldClrType: typeof(string),
                oldType: "nvarchar(20)",
                oldMaxLength: 20,
                oldNullable: true,
                oldCollation: Collation);

            migrationBuilder.CreateIndex(
                name: "IX_Setores_UnidadeHospitalarId_Nome",
                schema: "sge",
                table: "Setores",
                columns: new[] { "UnidadeHospitalarId", "Nome" },
                unique: true,
                filter: "[Excluido] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_Setores_CategoriaSetorId",
                schema: "sge",
                table: "Setores",
                column: "CategoriaSetorId");

            migrationBuilder.CreateIndex(
                name: "IX_Setores_ResponsavelFuncionarioId",
                schema: "sge",
                table: "Setores",
                column: "ResponsavelFuncionarioId");

            migrationBuilder.CreateIndex(
                name: "IX_Setores_UnidadeHospitalarId_Sigla",
                schema: "sge",
                table: "Setores",
                columns: new[] { "UnidadeHospitalarId", "Sigla" },
                unique: true,
                filter: "[Excluido] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_CategoriasSetores_Guid",
                schema: "sge",
                table: "CategoriasSetores",
                column: "Guid",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CategoriasSetores_Nome",
                schema: "sge",
                table: "CategoriasSetores",
                column: "Nome",
                unique: true,
                filter: "[Excluido] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_SetoresUnidadesAtendidas_Guid",
                schema: "sge",
                table: "SetoresUnidadesAtendidas",
                column: "Guid",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SetoresUnidadesAtendidas_SetorId_UnidadeHospitalarId",
                schema: "sge",
                table: "SetoresUnidadesAtendidas",
                columns: new[] { "SetorId", "UnidadeHospitalarId" },
                unique: true,
                filter: "[Ativo] = 1 AND [Excluido] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_SetoresUnidadesAtendidas_UnidadeHospitalarId",
                schema: "sge",
                table: "SetoresUnidadesAtendidas",
                column: "UnidadeHospitalarId");

            migrationBuilder.AddForeignKey(
                name: "FK_Setores_CategoriasSetores_CategoriaSetorId",
                schema: "sge",
                table: "Setores",
                column: "CategoriaSetorId",
                principalSchema: "sge",
                principalTable: "CategoriasSetores",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Setores_Funcionarios_ResponsavelFuncionarioId",
                schema: "sge",
                table: "Setores",
                column: "ResponsavelFuncionarioId",
                principalSchema: "sge",
                principalTable: "Funcionarios",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Setores_CategoriasSetores_CategoriaSetorId",
                schema: "sge",
                table: "Setores");

            migrationBuilder.DropForeignKey(
                name: "FK_Setores_Funcionarios_ResponsavelFuncionarioId",
                schema: "sge",
                table: "Setores");

            migrationBuilder.DropTable(
                name: "SetoresUnidadesAtendidas",
                schema: "sge");

            migrationBuilder.DropTable(
                name: "CategoriasSetores",
                schema: "sge");

            migrationBuilder.DropIndex(
                name: "IX_Setores_CategoriaSetorId",
                schema: "sge",
                table: "Setores");

            migrationBuilder.DropIndex(
                name: "IX_Setores_ResponsavelFuncionarioId",
                schema: "sge",
                table: "Setores");

            migrationBuilder.DropIndex(
                name: "IX_Setores_UnidadeHospitalarId_Sigla",
                schema: "sge",
                table: "Setores");

            migrationBuilder.DropIndex(
                name: "IX_Setores_UnidadeHospitalarId_Nome",
                schema: "sge",
                table: "Setores");

            migrationBuilder.DeleteData(schema: "sge", table: "Permissoes", keyColumn: "Id", keyValue: 14L);
            migrationBuilder.DeleteData(schema: "sge", table: "Permissoes", keyColumn: "Id", keyValue: 15L);
            migrationBuilder.DeleteData(schema: "sge", table: "Permissoes", keyColumn: "Id", keyValue: 16L);
            migrationBuilder.DeleteData(schema: "sge", table: "Permissoes", keyColumn: "Id", keyValue: 17L);

            migrationBuilder.DropColumn(name: "Assistencial", schema: "sge", table: "Setores");
            migrationBuilder.DropColumn(name: "CategoriaSetorId", schema: "sge", table: "Setores");
            migrationBuilder.DropColumn(name: "Descricao", schema: "sge", table: "Setores");
            migrationBuilder.DropColumn(name: "Email", schema: "sge", table: "Setores");
            migrationBuilder.DropColumn(name: "LocalizacaoInterna", schema: "sge", table: "Setores");
            migrationBuilder.DropColumn(name: "PermiteAlocacaoEscala", schema: "sge", table: "Setores");
            migrationBuilder.DropColumn(name: "PermiteAtuacaoCompartilhada", schema: "sge", table: "Setores");
            migrationBuilder.DropColumn(name: "Ramal", schema: "sge", table: "Setores");
            migrationBuilder.DropColumn(name: "ResponsavelFuncionarioId", schema: "sge", table: "Setores");
            migrationBuilder.DropColumn(name: "Sigla", schema: "sge", table: "Setores");

            migrationBuilder.AlterColumn<string>(
                name: "Nome",
                schema: "sge",
                table: "Setores",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(150)",
                oldMaxLength: 150,
                oldCollation: Collation);

            migrationBuilder.CreateIndex(
                name: "IX_Setores_UnidadeHospitalarId_Nome",
                schema: "sge",
                table: "Setores",
                columns: new[] { "UnidadeHospitalarId", "Nome" },
                unique: true,
                filter: "[Excluido] = 0");
        }
    }
}
