using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sistema.Gestao.Empresarial.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ReliableOutboxPublisher : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_OutboxMessages_Status_OccurredAt",
                schema: "sge",
                table: "OutboxMessages");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "BloqueadoAte",
                schema: "sge",
                table: "OutboxMessages",
                type: "datetimeoffset(3)",
                precision: 3,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "LockId",
                schema: "sge",
                table: "OutboxMessages",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ProximaTentativaEm",
                schema: "sge",
                table: "OutboxMessages",
                type: "datetimeoffset(3)",
                precision: 3,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "UltimaTentativaEm",
                schema: "sge",
                table: "OutboxMessages",
                type: "datetimeoffset(3)",
                precision: 3,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WorkerId",
                schema: "sge",
                table: "OutboxMessages",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessages_Status_ProximaTentativaEm_BloqueadoAte_OccurredAt",
                schema: "sge",
                table: "OutboxMessages",
                columns: new[] { "Status", "ProximaTentativaEm", "BloqueadoAte", "OccurredAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            throw new NotSupportedException(
                "Rollback bloqueado: mensagens e histórico da Outbox não podem ser fisicamente excluídos.");
        }
    }
}
