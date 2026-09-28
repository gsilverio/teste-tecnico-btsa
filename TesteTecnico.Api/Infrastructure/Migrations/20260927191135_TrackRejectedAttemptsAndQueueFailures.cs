using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TesteTecnico.Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class TrackRejectedAttemptsAndQueueFailures : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DeadLetteredAt",
                table: "transfer_outbox_messages",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastProcessingError",
                table: "transfer_outbox_messages",
                type: "character varying(2048)",
                maxLength: 2048,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ProcessingAttempts",
                table: "transfer_outbox_messages",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddCheckConstraint(
                name: "ck_transfer_outbox_processing_attempts_nonnegative",
                table: "transfer_outbox_messages",
                sql: "\"ProcessingAttempts\" >= 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_transfer_outbox_processing_attempts_nonnegative",
                table: "transfer_outbox_messages");

            migrationBuilder.DropColumn(
                name: "DeadLetteredAt",
                table: "transfer_outbox_messages");

            migrationBuilder.DropColumn(
                name: "LastProcessingError",
                table: "transfer_outbox_messages");

            migrationBuilder.DropColumn(
                name: "ProcessingAttempts",
                table: "transfer_outbox_messages");
        }
    }
}
