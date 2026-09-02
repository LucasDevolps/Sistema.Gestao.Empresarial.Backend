using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sistema.Gestao.Empresarial.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class EmployeeMultiHospitalUseCases : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_FuncionariosUnidadesAtuacao_FuncionarioId_UnidadeHospitalarId_Ativo",
                schema: "sge",
                table: "FuncionariosUnidadesAtuacao");

            migrationBuilder.DropIndex(
                name: "IX_FuncionariosSetores_FuncionarioId_SetorId_Ativo",
                schema: "sge",
                table: "FuncionariosSetores");

            migrationBuilder.DropIndex(
                name: "IX_Funcionarios_Email",
                schema: "sge",
                table: "Funcionarios");

            migrationBuilder.CreateIndex(
                name: "IX_FuncionariosUnidadesAtuacao_FuncionarioId_UnidadeHospitalarId",
                schema: "sge",
                table: "FuncionariosUnidadesAtuacao",
                columns: new[] { "FuncionarioId", "UnidadeHospitalarId" },
                unique: true,
                filter: "[Ativo] = 1 AND [Excluido] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_FuncionariosSetores_FuncionarioId_SetorId",
                schema: "sge",
                table: "FuncionariosSetores",
                columns: new[] { "FuncionarioId", "SetorId" },
                unique: true,
                filter: "[Ativo] = 1 AND [Excluido] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_Funcionarios_Email",
                schema: "sge",
                table: "Funcionarios",
                column: "Email",
                unique: true,
                filter: "[Excluido] = 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_FuncionariosUnidadesAtuacao_FuncionarioId_UnidadeHospitalarId",
                schema: "sge",
                table: "FuncionariosUnidadesAtuacao");

            migrationBuilder.DropIndex(
                name: "IX_FuncionariosSetores_FuncionarioId_SetorId",
                schema: "sge",
                table: "FuncionariosSetores");

            migrationBuilder.DropIndex(
                name: "IX_Funcionarios_Email",
                schema: "sge",
                table: "Funcionarios");

            migrationBuilder.CreateIndex(
                name: "IX_FuncionariosUnidadesAtuacao_FuncionarioId_UnidadeHospitalarId_Ativo",
                schema: "sge",
                table: "FuncionariosUnidadesAtuacao",
                columns: new[] { "FuncionarioId", "UnidadeHospitalarId", "Ativo" },
                filter: "[Excluido] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_FuncionariosSetores_FuncionarioId_SetorId_Ativo",
                schema: "sge",
                table: "FuncionariosSetores",
                columns: new[] { "FuncionarioId", "SetorId", "Ativo" },
                filter: "[Excluido] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_Funcionarios_Email",
                schema: "sge",
                table: "Funcionarios",
                column: "Email",
                filter: "[Excluido] = 0");
        }
    }
}
