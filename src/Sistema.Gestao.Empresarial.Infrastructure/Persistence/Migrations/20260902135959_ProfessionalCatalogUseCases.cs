using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Sistema.Gestao.Empresarial.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ProfessionalCatalogUseCases : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                schema: "sge",
                table: "Permissoes",
                columns: new[] { "Id", "Ativo", "Codigo", "DataAtualizacao", "DataCriacao", "Descricao", "Excluido", "ExcluidoEm", "ExcluidoPor", "Guid" },
                values: new object[,]
                {
                    { 9L, true, "PROFISSAO_EDITAR", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Editar profissões", false, null, null, new Guid("659c28fd-a7d5-489e-bd32-c809601ee3f0") },
                    { 10L, true, "CARGO_VISUALIZAR", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Visualizar cargos", false, null, null, new Guid("b21b733d-30f3-4f28-8477-d5ce7517ae96") },
                    { 11L, true, "CARGO_CRIAR", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Criar cargos", false, null, null, new Guid("80a43a7a-4615-49c5-bba8-08488439c687") },
                    { 12L, true, "CARGO_EDITAR", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Editar cargos", false, null, null, new Guid("f45d589b-3234-4334-8cb7-15c4ca1ae245") },
                    { 13L, true, "NIVEL_PROFISSIONAL_VISUALIZAR", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Visualizar níveis profissionais", false, null, null, new Guid("8f611dfc-3af0-4fc0-9a97-ee76de6e846d") }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                schema: "sge",
                table: "Permissoes",
                keyColumn: "Id",
                keyValue: 9L);

            migrationBuilder.DeleteData(
                schema: "sge",
                table: "Permissoes",
                keyColumn: "Id",
                keyValue: 10L);

            migrationBuilder.DeleteData(
                schema: "sge",
                table: "Permissoes",
                keyColumn: "Id",
                keyValue: 11L);

            migrationBuilder.DeleteData(
                schema: "sge",
                table: "Permissoes",
                keyColumn: "Id",
                keyValue: 12L);

            migrationBuilder.DeleteData(
                schema: "sge",
                table: "Permissoes",
                keyColumn: "Id",
                keyValue: 13L);
        }
    }
}
