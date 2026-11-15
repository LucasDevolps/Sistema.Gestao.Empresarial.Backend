using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sistema.Gestao.Empresarial.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class JornadaTrabalhoCicloDentroDoMaximo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Jornadas gravadas antes desta regra (ex.: 7x1 com máximo 6) violariam a
            // constraint. Em vez de corrigi-las silenciosamente — o que mudaria a regra de
            // negócio cadastrada pelo gestor — a migration falha listando-as para saneamento.
            migrationBuilder.Sql(@"
DECLARE @inconsistentes nvarchar(max);
DECLARE @mensagem nvarchar(2048);

SELECT @inconsistentes = STRING_AGG(
    CONVERT(nvarchar(max), CONCAT(QUOTENAME([Nome]), N' (', [DiasConsecutivosTrabalho], N'x', [DiasDescansoCiclo],
        N', máximo ', [MaximoDiasConsecutivos], N')')),
    N', ')
FROM [sge].[JornadasTrabalho]
WHERE [DiasConsecutivosTrabalho] > [MaximoDiasConsecutivos];

IF @inconsistentes IS NOT NULL
BEGIN
    SET @mensagem = LEFT(CONCAT(
        N'Existem jornadas de trabalho cujo ciclo excede o máximo de dias consecutivos. ',
        N'Saneie os registros antes de aplicar a migration: ', @inconsistentes), 2048);
    THROW 50001, @mensagem, 1;
END;
");

            migrationBuilder.AddCheckConstraint(
                name: "CK_JornadasTrabalho_CicloDentroDoMaximo",
                schema: "sge",
                table: "JornadasTrabalho",
                sql: "[DiasConsecutivosTrabalho] <= [MaximoDiasConsecutivos]");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_JornadasTrabalho_CicloDentroDoMaximo",
                schema: "sge",
                table: "JornadasTrabalho");
        }
    }
}
