using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TesteTecnico.Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RecordRejectedTransferAttempts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_transfer_attempts_TransferId",
                table: "transfer_attempts");

            migrationBuilder.AlterColumn<Guid>(
                name: "TransferId",
                table: "transfer_attempts",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<string>(
                name: "FailureCode",
                table: "transfer_attempts",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FailureMessage",
                table: "transfer_attempts",
                type: "character varying(2048)",
                maxLength: 2048,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IdempotencyKey",
                table: "transfer_attempts",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RequestFingerprint",
                table: "transfer_attempts",
                type: "character(64)",
                fixedLength: true,
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_transfer_attempts_SourceAccountId_IdempotencyKey",
                table: "transfer_attempts",
                columns: new[] { "SourceAccountId", "IdempotencyKey" },
                unique: true,
                filter: "\"IdempotencyKey\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_transfer_attempts_TransferId",
                table: "transfer_attempts",
                column: "TransferId",
                unique: true,
                filter: "\"TransferId\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DELETE FROM transfer_attempts WHERE \"TransferId\" IS NULL;");

            migrationBuilder.DropIndex(
                name: "IX_transfer_attempts_SourceAccountId_IdempotencyKey",
                table: "transfer_attempts");

            migrationBuilder.DropIndex(
                name: "IX_transfer_attempts_TransferId",
                table: "transfer_attempts");

            migrationBuilder.DropColumn(
                name: "FailureCode",
                table: "transfer_attempts");

            migrationBuilder.DropColumn(
                name: "FailureMessage",
                table: "transfer_attempts");

            migrationBuilder.DropColumn(
                name: "IdempotencyKey",
                table: "transfer_attempts");

            migrationBuilder.DropColumn(
                name: "RequestFingerprint",
                table: "transfer_attempts");

            migrationBuilder.AlterColumn<Guid>(
                name: "TransferId",
                table: "transfer_attempts",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_transfer_attempts_TransferId",
                table: "transfer_attempts",
                column: "TransferId",
                unique: true);
        }
    }
}
