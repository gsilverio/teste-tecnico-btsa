using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TesteTecnico.Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ImplementTransferProcessing : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "IdempotencyKey",
                table: "transfers",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RequestFingerprint",
                table: "transfers",
                type: "character(64)",
                fixedLength: true,
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "transfer_attempts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TransferId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceAccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    AttemptedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_transfer_attempts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_transfer_attempts_accounts_SourceAccountId",
                        column: x => x.SourceAccountId,
                        principalTable: "accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_transfer_attempts_transfers_TransferId",
                        column: x => x.TransferId,
                        principalTable: "transfers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "transfer_outbox_messages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TransferId = table.Column<Guid>(type: "uuid", nullable: false),
                    AvailableAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    PublishedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_transfer_outbox_messages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_transfer_outbox_messages_transfers_TransferId",
                        column: x => x.TransferId,
                        principalTable: "transfers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_transfers_SourceAccountId_IdempotencyKey",
                table: "transfers",
                columns: new[] { "SourceAccountId", "IdempotencyKey" },
                unique: true,
                filter: "\"IdempotencyKey\" IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "ck_transfers_idempotency_values_paired",
                table: "transfers",
                sql: "(\"IdempotencyKey\" IS NULL) = (\"RequestFingerprint\" IS NULL)");

            migrationBuilder.CreateIndex(
                name: "IX_transfer_attempts_SourceAccountId_AttemptedAt",
                table: "transfer_attempts",
                columns: new[] { "SourceAccountId", "AttemptedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_transfer_attempts_TransferId",
                table: "transfer_attempts",
                column: "TransferId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_transfer_outbox_messages_PublishedAt_AvailableAt",
                table: "transfer_outbox_messages",
                columns: new[] { "PublishedAt", "AvailableAt" });

            migrationBuilder.CreateIndex(
                name: "IX_transfer_outbox_messages_TransferId",
                table: "transfer_outbox_messages",
                column: "TransferId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "transfer_attempts");

            migrationBuilder.DropTable(
                name: "transfer_outbox_messages");

            migrationBuilder.DropIndex(
                name: "IX_transfers_SourceAccountId_IdempotencyKey",
                table: "transfers");

            migrationBuilder.DropCheckConstraint(
                name: "ck_transfers_idempotency_values_paired",
                table: "transfers");

            migrationBuilder.DropColumn(
                name: "IdempotencyKey",
                table: "transfers");

            migrationBuilder.DropColumn(
                name: "RequestFingerprint",
                table: "transfers");
        }
    }
}
