using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Sistema.Gestao.Empresarial.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class JornadasTrabalho : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "JornadasTrabalho",
                schema: "sge",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Nome = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false, collation: "Latin1_General_CI_AS"),
                    DiasConsecutivosTrabalho = table.Column<int>(type: "int", nullable: false),
                    DiasDescansoCiclo = table.Column<int>(type: "int", nullable: false),
                    MaximoDiasConsecutivos = table.Column<int>(type: "int", nullable: false),
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
                    table.PrimaryKey("PK_JornadasTrabalho", x => x.Id);
                    table.CheckConstraint("CK_JornadasTrabalho_DiasConsecutivosTrabalho", "[DiasConsecutivosTrabalho] > 0");
                    table.CheckConstraint("CK_JornadasTrabalho_DiasDescansoCiclo", "[DiasDescansoCiclo] > 0");
                    table.CheckConstraint("CK_JornadasTrabalho_MaximoDiasConsecutivos", "[MaximoDiasConsecutivos] BETWEEN 1 AND 6");
                });

            migrationBuilder.InsertData(
                schema: "sge",
                table: "Permissoes",
                columns: new[] { "Id", "Ativo", "Codigo", "DataAtualizacao", "DataCriacao", "Descricao", "Excluido", "ExcluidoEm", "ExcluidoPor", "Guid" },
                values: new object[,]
                {
                    { 23L, true, "JORNADA_TRABALHO_VISUALIZAR", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Visualizar jornadas de trabalho", false, null, null, new Guid("b3c1d5e7-2a4f-4c68-9e1b-7d0a5f3c8e24") },
                    { 24L, true, "JORNADA_TRABALHO_CRIAR", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Criar jornadas de trabalho", false, null, null, new Guid("c4d2e6f8-3b5a-4d79-8f2c-8e1b6a4d9f35") },
                    { 25L, true, "JORNADA_TRABALHO_EDITAR", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Editar, inativar e reativar jornadas de trabalho", false, null, null, new Guid("d5e3f7a9-4c6b-4e8a-9a3d-9f2c7b5eaa46") }
                });

            migrationBuilder.CreateIndex(
                name: "IX_JornadasTrabalho_Guid",
                schema: "sge",
                table: "JornadasTrabalho",
                column: "Guid",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_JornadasTrabalho_Nome",
                schema: "sge",
                table: "JornadasTrabalho",
                column: "Nome",
                unique: true,
                filter: "[Excluido] = 0");
            migrationBuilder.Sql(AdministratorProfilePermissionBackfill.GrantWorkSchedulePermissionsSql);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(AdministratorProfilePermissionBackfill.RevokeWorkSchedulePermissionsSql);
            migrationBuilder.DropTable(
                name: "JornadasTrabalho",
                schema: "sge");

            migrationBuilder.DeleteData(
                schema: "sge",
                table: "Permissoes",
                keyColumn: "Id",
                keyValue: 23L);

            migrationBuilder.DeleteData(
                schema: "sge",
                table: "Permissoes",
                keyColumn: "Id",
                keyValue: 24L);

            migrationBuilder.DeleteData(
                schema: "sge",
                table: "Permissoes",
                keyColumn: "Id",
                keyValue: 25L);
        }
    }
}
