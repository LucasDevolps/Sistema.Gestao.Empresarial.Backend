using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Sistema.Gestao.Empresarial.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CompleteHospitalUnitRegistration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_UnidadesHospitalares_OrganizacaoId_Nome",
                schema: "sge",
                table: "UnidadesHospitalares");

            migrationBuilder.AddColumn<string>(
                name: "AlvaraSanitario",
                schema: "sge",
                table: "UnidadesHospitalares",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "Atendimento24h",
                schema: "sge",
                table: "UnidadesHospitalares",
                type: "bit",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "AtendimentoAmbulatorial",
                schema: "sge",
                table: "UnidadesHospitalares",
                type: "bit",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Bairro",
                schema: "sge",
                table: "UnidadesHospitalares",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "CentroCirurgico",
                schema: "sge",
                table: "UnidadesHospitalares",
                type: "bit",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Cep",
                schema: "sge",
                table: "UnidadesHospitalares",
                type: "nvarchar(8)",
                maxLength: 8,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Cidade",
                schema: "sge",
                table: "UnidadesHospitalares",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CnaePrincipal",
                schema: "sge",
                table: "UnidadesHospitalares",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CnaesSecundarios",
                schema: "sge",
                table: "UnidadesHospitalares",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Cnes",
                schema: "sge",
                table: "UnidadesHospitalares",
                type: "nvarchar(7)",
                maxLength: 7,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Cnpj",
                schema: "sge",
                table: "UnidadesHospitalares",
                type: "nvarchar(14)",
                maxLength: 14,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CodigoIbge",
                schema: "sge",
                table: "UnidadesHospitalares",
                type: "nvarchar(7)",
                maxLength: 7,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CodigoInterno",
                schema: "sge",
                table: "UnidadesHospitalares",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true,
                collation: "Latin1_General_CI_AS");

            migrationBuilder.AddColumn<string>(
                name: "Complemento",
                schema: "sge",
                table: "UnidadesHospitalares",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "DataAbertura",
                schema: "sge",
                table: "UnidadesHospitalares",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Ddd",
                schema: "sge",
                table: "UnidadesHospitalares",
                type: "nvarchar(2)",
                maxLength: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DiretorClinicoCrm",
                schema: "sge",
                table: "UnidadesHospitalares",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DiretorClinicoEmail",
                schema: "sge",
                table: "UnidadesHospitalares",
                type: "nvarchar(254)",
                maxLength: 254,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DiretorClinicoNome",
                schema: "sge",
                table: "UnidadesHospitalares",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DiretorClinicoTelefone",
                schema: "sge",
                table: "UnidadesHospitalares",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DiretorClinicoUf",
                schema: "sge",
                table: "UnidadesHospitalares",
                type: "nvarchar(2)",
                maxLength: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EmailAdministrativo",
                schema: "sge",
                table: "UnidadesHospitalares",
                type: "nvarchar(254)",
                maxLength: 254,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EmailInstitucional",
                schema: "sge",
                table: "UnidadesHospitalares",
                type: "nvarchar(254)",
                maxLength: 254,
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "InicioAtividades",
                schema: "sge",
                table: "UnidadesHospitalares",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InscricaoEstadual",
                schema: "sge",
                table: "UnidadesHospitalares",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InscricaoMunicipal",
                schema: "sge",
                table: "UnidadesHospitalares",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "Internacao",
                schema: "sge",
                table: "UnidadesHospitalares",
                type: "bit",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LeitosUti",
                schema: "sge",
                table: "UnidadesHospitalares",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LicencaFuncionamento",
                schema: "sge",
                table: "UnidadesHospitalares",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Logradouro",
                schema: "sge",
                table: "UnidadesHospitalares",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "Maternidade",
                schema: "sge",
                table: "UnidadesHospitalares",
                type: "bit",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Natureza",
                schema: "sge",
                table: "UnidadesHospitalares",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NaturezaJuridica",
                schema: "sge",
                table: "UnidadesHospitalares",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Numero",
                schema: "sge",
                table: "UnidadesHospitalares",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ObservacoesGerais",
                schema: "sge",
                table: "UnidadesHospitalares",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ObservacoesRegulatorias",
                schema: "sge",
                table: "UnidadesHospitalares",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "PossuiCnpjProprio",
                schema: "sge",
                table: "UnidadesHospitalares",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "ProntoSocorro",
                schema: "sge",
                table: "UnidadesHospitalares",
                type: "bit",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Ramal",
                schema: "sge",
                table: "UnidadesHospitalares",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RazaoSocial",
                schema: "sge",
                table: "UnidadesHospitalares",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReferenciaEndereco",
                schema: "sge",
                table: "UnidadesHospitalares",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Regiao",
                schema: "sge",
                table: "UnidadesHospitalares",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ResponsavelAdministrativoCargo",
                schema: "sge",
                table: "UnidadesHospitalares",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ResponsavelAdministrativoEmail",
                schema: "sge",
                table: "UnidadesHospitalares",
                type: "nvarchar(254)",
                maxLength: 254,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ResponsavelAdministrativoNome",
                schema: "sge",
                table: "UnidadesHospitalares",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ResponsavelAdministrativoTelefone",
                schema: "sge",
                table: "UnidadesHospitalares",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ResponsavelTecnicoConselho",
                schema: "sge",
                table: "UnidadesHospitalares",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ResponsavelTecnicoEmail",
                schema: "sge",
                table: "UnidadesHospitalares",
                type: "nvarchar(254)",
                maxLength: 254,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ResponsavelTecnicoNome",
                schema: "sge",
                table: "UnidadesHospitalares",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ResponsavelTecnicoProfissao",
                schema: "sge",
                table: "UnidadesHospitalares",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ResponsavelTecnicoRegistro",
                schema: "sge",
                table: "UnidadesHospitalares",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ResponsavelTecnicoTelefone",
                schema: "sge",
                table: "UnidadesHospitalares",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ResponsavelTecnicoUf",
                schema: "sge",
                table: "UnidadesHospitalares",
                type: "nvarchar(2)",
                maxLength: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Sigla",
                schema: "sge",
                table: "UnidadesHospitalares",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Site",
                schema: "sge",
                table: "UnidadesHospitalares",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SituacaoCadastral",
                schema: "sge",
                table: "UnidadesHospitalares",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TelefonePrincipal",
                schema: "sge",
                table: "UnidadesHospitalares",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TelefoneSecundario",
                schema: "sge",
                table: "UnidadesHospitalares",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Tipo",
                schema: "sge",
                table: "UnidadesHospitalares",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TotalLeitos",
                schema: "sge",
                table: "UnidadesHospitalares",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Uf",
                schema: "sge",
                table: "UnidadesHospitalares",
                type: "nvarchar(2)",
                maxLength: 2,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "Uti",
                schema: "sge",
                table: "UnidadesHospitalares",
                type: "bit",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "ValidadeAlvaraSanitario",
                schema: "sge",
                table: "UnidadesHospitalares",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "ValidadeLicencaFuncionamento",
                schema: "sge",
                table: "UnidadesHospitalares",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Whatsapp",
                schema: "sge",
                table: "UnidadesHospitalares",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.InsertData(
                schema: "sge",
                table: "Permissoes",
                columns: new[] { "Id", "Ativo", "Codigo", "DataAtualizacao", "DataCriacao", "Descricao", "Excluido", "ExcluidoEm", "ExcluidoPor", "Guid" },
                values: new object[,]
                {
                    { 20L, true, "UNIDADE_HOSPITALAR_VISUALIZAR", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Visualizar unidades hospitalares", false, null, null, new Guid("dc8b286a-4053-4083-903d-d3b021204fe4") },
                    { 21L, true, "UNIDADE_HOSPITALAR_CRIAR", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Criar unidades hospitalares", false, null, null, new Guid("556fc8dd-4b61-4d42-88a3-da101fd59af7") },
                    { 22L, true, "UNIDADE_HOSPITALAR_EDITAR", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Editar unidades hospitalares", false, null, null, new Guid("f72a18c9-5df5-401f-b5f9-8e39f3f00574") }
                });

            migrationBuilder.CreateIndex(
                name: "IX_UnidadesHospitalares_Cnes",
                schema: "sge",
                table: "UnidadesHospitalares",
                column: "Cnes",
                unique: true,
                filter: "[Cnes] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_UnidadesHospitalares_Cnpj",
                schema: "sge",
                table: "UnidadesHospitalares",
                column: "Cnpj",
                unique: true,
                filter: "[Cnpj] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_UnidadesHospitalares_OrganizacaoId_Cidade_Uf",
                schema: "sge",
                table: "UnidadesHospitalares",
                columns: new[] { "OrganizacaoId", "Cidade", "Uf" });

            migrationBuilder.CreateIndex(
                name: "IX_UnidadesHospitalares_OrganizacaoId_CodigoInterno",
                schema: "sge",
                table: "UnidadesHospitalares",
                columns: new[] { "OrganizacaoId", "CodigoInterno" },
                unique: true,
                filter: "[CodigoInterno] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_UnidadesHospitalares_OrganizacaoId_Nome",
                schema: "sge",
                table: "UnidadesHospitalares",
                columns: new[] { "OrganizacaoId", "Nome" });
            migrationBuilder.Sql(AdministratorProfilePermissionBackfill.GrantHospitalUnitPermissionsSql);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // O modelo anterior proibia nomes iguais. Nunca remover unidades para viabilizar rollback.
            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM [sge].[UnidadesHospitalares] WHERE [Excluido] = 0
    GROUP BY [OrganizacaoId], [Nome] HAVING COUNT(*) > 1)
    THROW 51048, 'Rollback indisponivel: existem nomes de unidades repetidos na mesma organizacao.', 1;");
            migrationBuilder.Sql(AdministratorProfilePermissionBackfill.RevokeHospitalUnitPermissionsSql);
            migrationBuilder.DropIndex(
                name: "IX_UnidadesHospitalares_Cnes",
                schema: "sge",
                table: "UnidadesHospitalares");

            migrationBuilder.DropIndex(
                name: "IX_UnidadesHospitalares_Cnpj",
                schema: "sge",
                table: "UnidadesHospitalares");

            migrationBuilder.DropIndex(
                name: "IX_UnidadesHospitalares_OrganizacaoId_Cidade_Uf",
                schema: "sge",
                table: "UnidadesHospitalares");

            migrationBuilder.DropIndex(
                name: "IX_UnidadesHospitalares_OrganizacaoId_CodigoInterno",
                schema: "sge",
                table: "UnidadesHospitalares");

            migrationBuilder.DropIndex(
                name: "IX_UnidadesHospitalares_OrganizacaoId_Nome",
                schema: "sge",
                table: "UnidadesHospitalares");

            migrationBuilder.DeleteData(
                schema: "sge",
                table: "Permissoes",
                keyColumn: "Id",
                keyValue: 20L);

            migrationBuilder.DeleteData(
                schema: "sge",
                table: "Permissoes",
                keyColumn: "Id",
                keyValue: 21L);

            migrationBuilder.DeleteData(
                schema: "sge",
                table: "Permissoes",
                keyColumn: "Id",
                keyValue: 22L);

            migrationBuilder.DropColumn(
                name: "AlvaraSanitario",
                schema: "sge",
                table: "UnidadesHospitalares");

            migrationBuilder.DropColumn(
                name: "Atendimento24h",
                schema: "sge",
                table: "UnidadesHospitalares");

            migrationBuilder.DropColumn(
                name: "AtendimentoAmbulatorial",
                schema: "sge",
                table: "UnidadesHospitalares");

            migrationBuilder.DropColumn(
                name: "Bairro",
                schema: "sge",
                table: "UnidadesHospitalares");

            migrationBuilder.DropColumn(
                name: "CentroCirurgico",
                schema: "sge",
                table: "UnidadesHospitalares");

            migrationBuilder.DropColumn(
                name: "Cep",
                schema: "sge",
                table: "UnidadesHospitalares");

            migrationBuilder.DropColumn(
                name: "Cidade",
                schema: "sge",
                table: "UnidadesHospitalares");

            migrationBuilder.DropColumn(
                name: "CnaePrincipal",
                schema: "sge",
                table: "UnidadesHospitalares");

            migrationBuilder.DropColumn(
                name: "CnaesSecundarios",
                schema: "sge",
                table: "UnidadesHospitalares");

            migrationBuilder.DropColumn(
                name: "Cnes",
                schema: "sge",
                table: "UnidadesHospitalares");

            migrationBuilder.DropColumn(
                name: "Cnpj",
                schema: "sge",
                table: "UnidadesHospitalares");

            migrationBuilder.DropColumn(
                name: "CodigoIbge",
                schema: "sge",
                table: "UnidadesHospitalares");

            migrationBuilder.DropColumn(
                name: "CodigoInterno",
                schema: "sge",
                table: "UnidadesHospitalares");

            migrationBuilder.DropColumn(
                name: "Complemento",
                schema: "sge",
                table: "UnidadesHospitalares");

            migrationBuilder.DropColumn(
                name: "DataAbertura",
                schema: "sge",
                table: "UnidadesHospitalares");

            migrationBuilder.DropColumn(
                name: "Ddd",
                schema: "sge",
                table: "UnidadesHospitalares");

            migrationBuilder.DropColumn(
                name: "DiretorClinicoCrm",
                schema: "sge",
                table: "UnidadesHospitalares");

            migrationBuilder.DropColumn(
                name: "DiretorClinicoEmail",
                schema: "sge",
                table: "UnidadesHospitalares");

            migrationBuilder.DropColumn(
                name: "DiretorClinicoNome",
                schema: "sge",
                table: "UnidadesHospitalares");

            migrationBuilder.DropColumn(
                name: "DiretorClinicoTelefone",
                schema: "sge",
                table: "UnidadesHospitalares");

            migrationBuilder.DropColumn(
                name: "DiretorClinicoUf",
                schema: "sge",
                table: "UnidadesHospitalares");

            migrationBuilder.DropColumn(
                name: "EmailAdministrativo",
                schema: "sge",
                table: "UnidadesHospitalares");

            migrationBuilder.DropColumn(
                name: "EmailInstitucional",
                schema: "sge",
                table: "UnidadesHospitalares");

            migrationBuilder.DropColumn(
                name: "InicioAtividades",
                schema: "sge",
                table: "UnidadesHospitalares");

            migrationBuilder.DropColumn(
                name: "InscricaoEstadual",
                schema: "sge",
                table: "UnidadesHospitalares");

            migrationBuilder.DropColumn(
                name: "InscricaoMunicipal",
                schema: "sge",
                table: "UnidadesHospitalares");

            migrationBuilder.DropColumn(
                name: "Internacao",
                schema: "sge",
                table: "UnidadesHospitalares");

            migrationBuilder.DropColumn(
                name: "LeitosUti",
                schema: "sge",
                table: "UnidadesHospitalares");

            migrationBuilder.DropColumn(
                name: "LicencaFuncionamento",
                schema: "sge",
                table: "UnidadesHospitalares");

            migrationBuilder.DropColumn(
                name: "Logradouro",
                schema: "sge",
                table: "UnidadesHospitalares");

            migrationBuilder.DropColumn(
                name: "Maternidade",
                schema: "sge",
                table: "UnidadesHospitalares");

            migrationBuilder.DropColumn(
                name: "Natureza",
                schema: "sge",
                table: "UnidadesHospitalares");

            migrationBuilder.DropColumn(
                name: "NaturezaJuridica",
                schema: "sge",
                table: "UnidadesHospitalares");

            migrationBuilder.DropColumn(
                name: "Numero",
                schema: "sge",
                table: "UnidadesHospitalares");

            migrationBuilder.DropColumn(
                name: "ObservacoesGerais",
                schema: "sge",
                table: "UnidadesHospitalares");

            migrationBuilder.DropColumn(
                name: "ObservacoesRegulatorias",
                schema: "sge",
                table: "UnidadesHospitalares");

            migrationBuilder.DropColumn(
                name: "PossuiCnpjProprio",
                schema: "sge",
                table: "UnidadesHospitalares");

            migrationBuilder.DropColumn(
                name: "ProntoSocorro",
                schema: "sge",
                table: "UnidadesHospitalares");

            migrationBuilder.DropColumn(
                name: "Ramal",
                schema: "sge",
                table: "UnidadesHospitalares");

            migrationBuilder.DropColumn(
                name: "RazaoSocial",
                schema: "sge",
                table: "UnidadesHospitalares");

            migrationBuilder.DropColumn(
                name: "ReferenciaEndereco",
                schema: "sge",
                table: "UnidadesHospitalares");

            migrationBuilder.DropColumn(
                name: "Regiao",
                schema: "sge",
                table: "UnidadesHospitalares");

            migrationBuilder.DropColumn(
                name: "ResponsavelAdministrativoCargo",
                schema: "sge",
                table: "UnidadesHospitalares");

            migrationBuilder.DropColumn(
                name: "ResponsavelAdministrativoEmail",
                schema: "sge",
                table: "UnidadesHospitalares");

            migrationBuilder.DropColumn(
                name: "ResponsavelAdministrativoNome",
                schema: "sge",
                table: "UnidadesHospitalares");

            migrationBuilder.DropColumn(
                name: "ResponsavelAdministrativoTelefone",
                schema: "sge",
                table: "UnidadesHospitalares");

            migrationBuilder.DropColumn(
                name: "ResponsavelTecnicoConselho",
                schema: "sge",
                table: "UnidadesHospitalares");

            migrationBuilder.DropColumn(
                name: "ResponsavelTecnicoEmail",
                schema: "sge",
                table: "UnidadesHospitalares");

            migrationBuilder.DropColumn(
                name: "ResponsavelTecnicoNome",
                schema: "sge",
                table: "UnidadesHospitalares");

            migrationBuilder.DropColumn(
                name: "ResponsavelTecnicoProfissao",
                schema: "sge",
                table: "UnidadesHospitalares");

            migrationBuilder.DropColumn(
                name: "ResponsavelTecnicoRegistro",
                schema: "sge",
                table: "UnidadesHospitalares");

            migrationBuilder.DropColumn(
                name: "ResponsavelTecnicoTelefone",
                schema: "sge",
                table: "UnidadesHospitalares");

            migrationBuilder.DropColumn(
                name: "ResponsavelTecnicoUf",
                schema: "sge",
                table: "UnidadesHospitalares");

            migrationBuilder.DropColumn(
                name: "Sigla",
                schema: "sge",
                table: "UnidadesHospitalares");

            migrationBuilder.DropColumn(
                name: "Site",
                schema: "sge",
                table: "UnidadesHospitalares");

            migrationBuilder.DropColumn(
                name: "SituacaoCadastral",
                schema: "sge",
                table: "UnidadesHospitalares");

            migrationBuilder.DropColumn(
                name: "TelefonePrincipal",
                schema: "sge",
                table: "UnidadesHospitalares");

            migrationBuilder.DropColumn(
                name: "TelefoneSecundario",
                schema: "sge",
                table: "UnidadesHospitalares");

            migrationBuilder.DropColumn(
                name: "Tipo",
                schema: "sge",
                table: "UnidadesHospitalares");

            migrationBuilder.DropColumn(
                name: "TotalLeitos",
                schema: "sge",
                table: "UnidadesHospitalares");

            migrationBuilder.DropColumn(
                name: "Uf",
                schema: "sge",
                table: "UnidadesHospitalares");

            migrationBuilder.DropColumn(
                name: "Uti",
                schema: "sge",
                table: "UnidadesHospitalares");

            migrationBuilder.DropColumn(
                name: "ValidadeAlvaraSanitario",
                schema: "sge",
                table: "UnidadesHospitalares");

            migrationBuilder.DropColumn(
                name: "ValidadeLicencaFuncionamento",
                schema: "sge",
                table: "UnidadesHospitalares");

            migrationBuilder.DropColumn(
                name: "Whatsapp",
                schema: "sge",
                table: "UnidadesHospitalares");

            migrationBuilder.CreateIndex(
                name: "IX_UnidadesHospitalares_OrganizacaoId_Nome",
                schema: "sge",
                table: "UnidadesHospitalares",
                columns: new[] { "OrganizacaoId", "Nome" },
                unique: true,
                filter: "[Excluido] = 0");
        }
    }
}
