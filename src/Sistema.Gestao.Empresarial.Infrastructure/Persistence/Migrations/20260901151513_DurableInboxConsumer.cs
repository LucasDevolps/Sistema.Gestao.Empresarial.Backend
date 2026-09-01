using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sistema.Gestao.Empresarial.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class DurableInboxConsumer : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "InboxMessages",
                schema: "sge",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MessageId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Consumer = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    EventType = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Payload = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CorrelationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TraceId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    RecebidoEm = table.Column<DateTimeOffset>(type: "datetimeoffset(3)", precision: 3, nullable: false),
                    ProcessadoEm = table.Column<DateTimeOffset>(type: "datetimeoffset(3)", precision: 3, nullable: true),
                    UltimaTentativaEm = table.Column<DateTimeOffset>(type: "datetimeoffset(3)", precision: 3, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Tentativas = table.Column<int>(type: "int", nullable: false),
                    Resultado = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Erro = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
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
                    table.PrimaryKey("PK_InboxMessages", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MessageAuditLogs",
                schema: "sge",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MessageId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EventType = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Consumer = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Tentativa = table.Column<int>(type: "int", nullable: false),
                    CorrelationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TraceId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    OcorridoEm = table.Column<DateTimeOffset>(type: "datetimeoffset(3)", precision: 3, nullable: false),
                    Detalhe = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
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
                    table.PrimaryKey("PK_MessageAuditLogs", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_InboxMessages_CorrelationId",
                schema: "sge",
                table: "InboxMessages",
                column: "CorrelationId");

            migrationBuilder.CreateIndex(
                name: "IX_InboxMessages_Guid",
                schema: "sge",
                table: "InboxMessages",
                column: "Guid",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InboxMessages_MessageId_Consumer",
                schema: "sge",
                table: "InboxMessages",
                columns: new[] { "MessageId", "Consumer" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InboxMessages_Status_RecebidoEm",
                schema: "sge",
                table: "InboxMessages",
                columns: new[] { "Status", "RecebidoEm" });

            migrationBuilder.CreateIndex(
                name: "IX_MessageAuditLogs_CorrelationId",
                schema: "sge",
                table: "MessageAuditLogs",
                column: "CorrelationId");

            migrationBuilder.CreateIndex(
                name: "IX_MessageAuditLogs_Guid",
                schema: "sge",
                table: "MessageAuditLogs",
                column: "Guid",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MessageAuditLogs_MessageId_Consumer_Tentativa",
                schema: "sge",
                table: "MessageAuditLogs",
                columns: new[] { "MessageId", "Consumer", "Tentativa" });

            migrationBuilder.CreateIndex(
                name: "IX_MessageAuditLogs_OcorridoEm",
                schema: "sge",
                table: "MessageAuditLogs",
                column: "OcorridoEm");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            throw new NotSupportedException(
                "Rollback bloqueado: Inbox e auditoria de mensagens não podem ser fisicamente excluídas.");
        }
    }
}
