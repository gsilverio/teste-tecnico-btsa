using System;
using Microsoft.EntityFrameworkCore.Migrations;
using TesteTecnico.Api.Domain.Audit;
using TesteTecnico.Api.Domain.Transfers;

#nullable disable

namespace TesteTecnico.Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAuditLogAndDomainEvents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:Enum:account_status", "active,blocked,inactive")
                .Annotation("Npgsql:Enum:audit_action_type", "cancel_transfer,clear_account_overdraft,create_transfer_limit_policy,delete_transfer_limit_policy,request_transfer,schedule_transfer,set_account_overdraft,set_account_status,transfer_cancelled,transfer_completed,transfer_failed,transfer_processing_started,transfer_requested,transfer_scheduled,update_transfer_limit_policy,view_account_details,view_account_overdraft")
                .Annotation("Npgsql:Enum:audit_source", "api,domain_event")
                .Annotation("Npgsql:Enum:bank_account_type", "checking,savings")
                .Annotation("Npgsql:Enum:pix_key_type", "cnpj,cpf,email,phone,random")
                .Annotation("Npgsql:Enum:transfer_method", "bank_account,pix")
                .Annotation("Npgsql:Enum:transfer_status", "cancelled,completed,failed,processing,scheduled")
                .OldAnnotation("Npgsql:Enum:account_status", "active,blocked,inactive")
                .OldAnnotation("Npgsql:Enum:bank_account_type", "checking,savings")
                .OldAnnotation("Npgsql:Enum:pix_key_type", "cnpj,cpf,email,phone,random")
                .OldAnnotation("Npgsql:Enum:transfer_method", "bank_account,pix")
                .OldAnnotation("Npgsql:Enum:transfer_status", "cancelled,completed,failed,processing,scheduled");

            migrationBuilder.AddColumn<int>(
                name: "AuditSequence",
                table: "transfers",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "audit_log",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Source = table.Column<AuditSource>(type: "audit_source", nullable: false),
                    ActionType = table.Column<AuditActionType>(type: "audit_action_type", nullable: false),
                    ActorId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    HttpMethod = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: true),
                    RouteTemplate = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    StatusCode = table.Column<int>(type: "integer", nullable: true),
                    TransferId = table.Column<Guid>(type: "uuid", nullable: true),
                    SourceAccountId = table.Column<Guid>(type: "uuid", nullable: true),
                    DestinationAccountId = table.Column<Guid>(type: "uuid", nullable: true),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    Method = table.Column<TransferMethod>(type: "transfer_method", nullable: true),
                    Sequence = table.Column<int>(type: "integer", nullable: true),
                    PreviousStatus = table.Column<TransferStatus>(type: "transfer_status", nullable: true),
                    Status = table.Column<TransferStatus>(type: "transfer_status", nullable: true),
                    OccurredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    PayloadJson = table.Column<string>(type: "jsonb", nullable: true),
                    ErrorMessage = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_audit_log", x => x.Id);
                    table.CheckConstraint("ck_audit_log_error_message_length", "\"ErrorMessage\" IS NULL OR length(\"ErrorMessage\") <= 2048");
                    table.CheckConstraint("ck_audit_log_positive_amount", "\"Amount\" IS NULL OR \"Amount\" > 0");
                });

            migrationBuilder.CreateIndex(
                name: "IX_audit_log_OccurredAt",
                table: "audit_log",
                column: "OccurredAt");

            migrationBuilder.CreateIndex(
                name: "IX_audit_log_TransferId_Sequence",
                table: "audit_log",
                columns: new[] { "TransferId", "Sequence" },
                unique: true,
                filter: "\"Sequence\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "audit_log");

            migrationBuilder.DropColumn(
                name: "AuditSequence",
                table: "transfers");

            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:Enum:account_status", "active,blocked,inactive")
                .Annotation("Npgsql:Enum:bank_account_type", "checking,savings")
                .Annotation("Npgsql:Enum:pix_key_type", "cnpj,cpf,email,phone,random")
                .Annotation("Npgsql:Enum:transfer_method", "bank_account,pix")
                .Annotation("Npgsql:Enum:transfer_status", "cancelled,completed,failed,processing,scheduled")
                .OldAnnotation("Npgsql:Enum:account_status", "active,blocked,inactive")
                .OldAnnotation("Npgsql:Enum:audit_action_type", "cancel_transfer,clear_account_overdraft,create_transfer_limit_policy,delete_transfer_limit_policy,request_transfer,schedule_transfer,set_account_overdraft,set_account_status,transfer_cancelled,transfer_completed,transfer_failed,transfer_processing_started,transfer_requested,transfer_scheduled,update_transfer_limit_policy,view_account_details,view_account_overdraft")
                .OldAnnotation("Npgsql:Enum:audit_source", "api,domain_event")
                .OldAnnotation("Npgsql:Enum:bank_account_type", "checking,savings")
                .OldAnnotation("Npgsql:Enum:pix_key_type", "cnpj,cpf,email,phone,random")
                .OldAnnotation("Npgsql:Enum:transfer_method", "bank_account,pix")
                .OldAnnotation("Npgsql:Enum:transfer_status", "cancelled,completed,failed,processing,scheduled");
        }
    }
}
