using System;
using Microsoft.EntityFrameworkCore.Migrations;
using TesteTecnico.Api.Domain.Accounts;
using TesteTecnico.Api.Domain.Transfers;

#nullable disable

namespace TesteTecnico.Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:Enum:account_status", "active,blocked,inactive")
                .Annotation("Npgsql:Enum:bank_account_type", "checking,savings")
                .Annotation("Npgsql:Enum:pix_key_type", "cnpj,cpf,email,phone,random")
                .Annotation("Npgsql:Enum:transfer_method", "bank_account,pix")
                .Annotation("Npgsql:Enum:transfer_status", "cancelled,completed,failed,processing,scheduled");

            migrationBuilder.CreateTable(
                name: "account_holders",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_account_holders", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "banks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Ispb = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    CompeCode = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_banks", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "accounts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerId = table.Column<Guid>(type: "uuid", nullable: false),
                    BankId = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<BankAccountType>(type: "bank_account_type", nullable: false),
                    Branch = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    Number = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CheckDigit = table.Column<string>(type: "character varying(1)", maxLength: 1, nullable: true),
                    Balance = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    OverdraftLimit = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Status = table.Column<AccountStatus>(type: "account_status", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_accounts", x => x.Id);
                    table.CheckConstraint("ck_accounts_balance_precision", "\"Balance\" = round(\"Balance\", 2)");
                    table.CheckConstraint("ck_accounts_balance_within_overdraft", "\"Balance\" >= -\"OverdraftLimit\"");
                    table.CheckConstraint("ck_accounts_overdraft_nonnegative", "\"OverdraftLimit\" >= 0");
                    table.ForeignKey(
                        name: "FK_accounts_account_holders_OwnerId",
                        column: x => x.OwnerId,
                        principalTable: "account_holders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_accounts_banks_BankId",
                        column: x => x.BankId,
                        principalTable: "banks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "account_pix_keys",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<PixKeyType>(type: "pix_key_type", nullable: false),
                    Value = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    AccountId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_account_pix_keys", x => x.Id);
                    table.ForeignKey(
                        name: "FK_account_pix_keys_accounts_AccountId",
                        column: x => x.AccountId,
                        principalTable: "accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "transfers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceAccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    DestinationAccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Method = table.Column<TransferMethod>(type: "transfer_method", nullable: false),
                    Status = table.Column<TransferStatus>(type: "transfer_status", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ScheduledAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    FinishedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    FailureCode = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_transfers", x => x.Id);
                    table.CheckConstraint("ck_transfers_amount_precision", "\"Amount\" = round(\"Amount\", 2)");
                    table.CheckConstraint("ck_transfers_different_accounts", "\"SourceAccountId\" <> \"DestinationAccountId\"");
                    table.CheckConstraint("ck_transfers_positive_amount", "\"Amount\" > 0");
                    table.ForeignKey(
                        name: "FK_transfers_accounts_DestinationAccountId",
                        column: x => x.DestinationAccountId,
                        principalTable: "accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_transfers_accounts_SourceAccountId",
                        column: x => x.SourceAccountId,
                        principalTable: "accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_account_pix_keys_AccountId",
                table: "account_pix_keys",
                column: "AccountId");

            migrationBuilder.CreateIndex(
                name: "IX_account_pix_keys_Value",
                table: "account_pix_keys",
                column: "Value",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_accounts_BankId_Branch_Number",
                table: "accounts",
                columns: new[] { "BankId", "Branch", "Number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_accounts_OwnerId",
                table: "accounts",
                column: "OwnerId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_banks_CompeCode",
                table: "banks",
                column: "CompeCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_banks_Ispb",
                table: "banks",
                column: "Ispb",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_transfers_DestinationAccountId",
                table: "transfers",
                column: "DestinationAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_transfers_SourceAccountId_CreatedAt",
                table: "transfers",
                columns: new[] { "SourceAccountId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_transfers_Status_ScheduledAt",
                table: "transfers",
                columns: new[] { "Status", "ScheduledAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "account_pix_keys");

            migrationBuilder.DropTable(
                name: "transfers");

            migrationBuilder.DropTable(
                name: "accounts");

            migrationBuilder.DropTable(
                name: "account_holders");

            migrationBuilder.DropTable(
                name: "banks");
        }
    }
}
