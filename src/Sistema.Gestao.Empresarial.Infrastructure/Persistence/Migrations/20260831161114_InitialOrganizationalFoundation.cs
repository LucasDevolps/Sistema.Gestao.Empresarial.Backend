using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Sistema.Gestao.Empresarial.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialOrganizationalFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "sge");

            migrationBuilder.CreateSequence(
                name: "SequenciaMatriculaFuncionario",
                schema: "sge");

            migrationBuilder.CreateTable(
                name: "Cargos",
                schema: "sge",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Nome = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
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
                    table.PrimaryKey("PK_Cargos", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "NiveisProfissionais",
                schema: "sge",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Codigo = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Nome = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Ordem = table.Column<int>(type: "int", nullable: false),
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
                    table.PrimaryKey("PK_NiveisProfissionais", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Organizacoes",
                schema: "sge",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Nome = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
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
                    table.PrimaryKey("PK_Organizacoes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Profissoes",
                schema: "sge",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Nome = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
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
                    table.PrimaryKey("PK_Profissoes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UnidadesHospitalares",
                schema: "sge",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OrganizacaoId = table.Column<long>(type: "bigint", nullable: false),
                    Nome = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
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
                    table.PrimaryKey("PK_UnidadesHospitalares", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UnidadesHospitalares_Organizacoes_OrganizacaoId",
                        column: x => x.OrganizacaoId,
                        principalSchema: "sge",
                        principalTable: "Organizacoes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Funcionarios",
                schema: "sge",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Matricula = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValueSql: "CONCAT('FUN', RIGHT(REPLICATE('0', 9) + CONVERT(varchar(20), NEXT VALUE FOR [sge].[SequenciaMatriculaFuncionario]), 9))"),
                    Nome = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Email = table.Column<string>(type: "nvarchar(254)", maxLength: 254, nullable: false),
                    Telefone = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    ProfissaoId = table.Column<long>(type: "bigint", nullable: false),
                    CargoId = table.Column<long>(type: "bigint", nullable: false),
                    NivelId = table.Column<long>(type: "bigint", nullable: false),
                    UnidadeContratacaoId = table.Column<long>(type: "bigint", nullable: false),
                    DataAdmissao = table.Column<DateOnly>(type: "date", nullable: false),
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
                    table.PrimaryKey("PK_Funcionarios", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Funcionarios_Cargos_CargoId",
                        column: x => x.CargoId,
                        principalSchema: "sge",
                        principalTable: "Cargos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Funcionarios_NiveisProfissionais_NivelId",
                        column: x => x.NivelId,
                        principalSchema: "sge",
                        principalTable: "NiveisProfissionais",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Funcionarios_Profissoes_ProfissaoId",
                        column: x => x.ProfissaoId,
                        principalSchema: "sge",
                        principalTable: "Profissoes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Funcionarios_UnidadesHospitalares_UnidadeContratacaoId",
                        column: x => x.UnidadeContratacaoId,
                        principalSchema: "sge",
                        principalTable: "UnidadesHospitalares",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Setores",
                schema: "sge",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UnidadeHospitalarId = table.Column<long>(type: "bigint", nullable: false),
                    Nome = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
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
                    table.PrimaryKey("PK_Setores", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Setores_UnidadesHospitalares_UnidadeHospitalarId",
                        column: x => x.UnidadeHospitalarId,
                        principalSchema: "sge",
                        principalTable: "UnidadesHospitalares",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FuncionariosUnidadesAtuacao",
                schema: "sge",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FuncionarioId = table.Column<long>(type: "bigint", nullable: false),
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
                    table.PrimaryKey("PK_FuncionariosUnidadesAtuacao", x => x.Id);
                    table.CheckConstraint("CK_FuncionarioUnidadeAtuacao_Periodo", "[DataFim] IS NULL OR [DataFim] >= [DataInicio]");
                    table.ForeignKey(
                        name: "FK_FuncionariosUnidadesAtuacao_Funcionarios_FuncionarioId",
                        column: x => x.FuncionarioId,
                        principalSchema: "sge",
                        principalTable: "Funcionarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FuncionariosUnidadesAtuacao_UnidadesHospitalares_UnidadeHospitalarId",
                        column: x => x.UnidadeHospitalarId,
                        principalSchema: "sge",
                        principalTable: "UnidadesHospitalares",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FuncionariosSetores",
                schema: "sge",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FuncionarioId = table.Column<long>(type: "bigint", nullable: false),
                    SetorId = table.Column<long>(type: "bigint", nullable: false),
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
                    table.PrimaryKey("PK_FuncionariosSetores", x => x.Id);
                    table.CheckConstraint("CK_FuncionarioSetor_Periodo", "[DataFim] IS NULL OR [DataFim] >= [DataInicio]");
                    table.ForeignKey(
                        name: "FK_FuncionariosSetores_Funcionarios_FuncionarioId",
                        column: x => x.FuncionarioId,
                        principalSchema: "sge",
                        principalTable: "Funcionarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FuncionariosSetores_Setores_SetorId",
                        column: x => x.SetorId,
                        principalSchema: "sge",
                        principalTable: "Setores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                schema: "sge",
                table: "NiveisProfissionais",
                columns: new[] { "Id", "Ativo", "Codigo", "DataAtualizacao", "DataCriacao", "Excluido", "ExcluidoEm", "ExcluidoPor", "Guid", "Nome", "Ordem" },
                values: new object[,]
                {
                    { 1L, true, "JR", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), false, null, null, new Guid("870d89d7-153a-46eb-93e4-a2e08e966d19"), "Júnior", 1 },
                    { 2L, true, "PL", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), false, null, null, new Guid("9d77bfd3-dc47-44e5-a4ca-b62497cd5864"), "Pleno", 2 },
                    { 3L, true, "SR", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), false, null, null, new Guid("e6e15ae5-ff9b-4a07-884a-5e66f805bfe0"), "Sênior", 3 }
                });

            migrationBuilder.CreateIndex(
                name: "IX_Cargos_Guid",
                schema: "sge",
                table: "Cargos",
                column: "Guid",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Cargos_Nome",
                schema: "sge",
                table: "Cargos",
                column: "Nome",
                unique: true,
                filter: "[Excluido] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_Funcionarios_CargoId",
                schema: "sge",
                table: "Funcionarios",
                column: "CargoId");

            migrationBuilder.CreateIndex(
                name: "IX_Funcionarios_Email",
                schema: "sge",
                table: "Funcionarios",
                column: "Email",
                filter: "[Excluido] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_Funcionarios_Guid",
                schema: "sge",
                table: "Funcionarios",
                column: "Guid",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Funcionarios_Matricula",
                schema: "sge",
                table: "Funcionarios",
                column: "Matricula",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Funcionarios_NivelId",
                schema: "sge",
                table: "Funcionarios",
                column: "NivelId");

            migrationBuilder.CreateIndex(
                name: "IX_Funcionarios_ProfissaoId",
                schema: "sge",
                table: "Funcionarios",
                column: "ProfissaoId");

            migrationBuilder.CreateIndex(
                name: "IX_Funcionarios_UnidadeContratacaoId",
                schema: "sge",
                table: "Funcionarios",
                column: "UnidadeContratacaoId");

            migrationBuilder.CreateIndex(
                name: "IX_FuncionariosSetores_FuncionarioId_SetorId_Ativo",
                schema: "sge",
                table: "FuncionariosSetores",
                columns: new[] { "FuncionarioId", "SetorId", "Ativo" },
                filter: "[Excluido] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_FuncionariosSetores_Guid",
                schema: "sge",
                table: "FuncionariosSetores",
                column: "Guid",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FuncionariosSetores_SetorId",
                schema: "sge",
                table: "FuncionariosSetores",
                column: "SetorId");

            migrationBuilder.CreateIndex(
                name: "IX_FuncionariosUnidadesAtuacao_FuncionarioId_UnidadeHospitalarId_Ativo",
                schema: "sge",
                table: "FuncionariosUnidadesAtuacao",
                columns: new[] { "FuncionarioId", "UnidadeHospitalarId", "Ativo" },
                filter: "[Excluido] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_FuncionariosUnidadesAtuacao_Guid",
                schema: "sge",
                table: "FuncionariosUnidadesAtuacao",
                column: "Guid",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FuncionariosUnidadesAtuacao_UnidadeHospitalarId",
                schema: "sge",
                table: "FuncionariosUnidadesAtuacao",
                column: "UnidadeHospitalarId");

            migrationBuilder.CreateIndex(
                name: "IX_NiveisProfissionais_Codigo",
                schema: "sge",
                table: "NiveisProfissionais",
                column: "Codigo",
                unique: true,
                filter: "[Excluido] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_NiveisProfissionais_Guid",
                schema: "sge",
                table: "NiveisProfissionais",
                column: "Guid",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Organizacoes_Guid",
                schema: "sge",
                table: "Organizacoes",
                column: "Guid",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Organizacoes_Nome",
                schema: "sge",
                table: "Organizacoes",
                column: "Nome",
                unique: true,
                filter: "[Excluido] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_Profissoes_Guid",
                schema: "sge",
                table: "Profissoes",
                column: "Guid",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Profissoes_Nome",
                schema: "sge",
                table: "Profissoes",
                column: "Nome",
                unique: true,
                filter: "[Excluido] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_Setores_Guid",
                schema: "sge",
                table: "Setores",
                column: "Guid",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Setores_UnidadeHospitalarId_Nome",
                schema: "sge",
                table: "Setores",
                columns: new[] { "UnidadeHospitalarId", "Nome" },
                unique: true,
                filter: "[Excluido] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_UnidadesHospitalares_Guid",
                schema: "sge",
                table: "UnidadesHospitalares",
                column: "Guid",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UnidadesHospitalares_OrganizacaoId_Nome",
                schema: "sge",
                table: "UnidadesHospitalares",
                columns: new[] { "OrganizacaoId", "Nome" },
                unique: true,
                filter: "[Excluido] = 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            throw new NotSupportedException(
                "Rollback bloqueado: dados organizacionais não podem ser fisicamente excluídos.");
        }
    }
}
