using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sistema.Gestao.Empresarial.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class EmployeeScaleParameters : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "ParticipaDaEscala",
                schema: "sge",
                table: "Funcionarios",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "Produtividade",
                schema: "sge",
                table: "Funcionarios",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Funcionarios_Produtividade",
                schema: "sge",
                table: "Funcionarios",
                sql: "[Produtividade] IN (0, 1, 2)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Funcionarios_Produtividade",
                schema: "sge",
                table: "Funcionarios");

            migrationBuilder.DropColumn(
                name: "ParticipaDaEscala",
                schema: "sge",
                table: "Funcionarios");

            migrationBuilder.DropColumn(
                name: "Produtividade",
                schema: "sge",
                table: "Funcionarios");
        }
    }
}
