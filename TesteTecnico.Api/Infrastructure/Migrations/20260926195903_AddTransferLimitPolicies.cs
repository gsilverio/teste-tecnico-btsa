using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TesteTecnico.Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTransferLimitPolicies : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "transfer_limit_policies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AccountHolderId = table.Column<Guid>(type: "uuid", nullable: false),
                    DayMaximumAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    DayMaximumAttempts = table.Column<int>(type: "integer", nullable: false),
                    NightMaximumAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    NightMaximumAttempts = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_transfer_limit_policies", x => x.Id);
                    table.CheckConstraint("ck_transfer_limit_policies_day_amount_nonnegative", "\"DayMaximumAmount\" >= 0");
                    table.CheckConstraint("ck_transfer_limit_policies_day_attempts_nonnegative", "\"DayMaximumAttempts\" >= 0");
                    table.CheckConstraint("ck_transfer_limit_policies_night_amount_nonnegative", "\"NightMaximumAmount\" >= 0");
                    table.CheckConstraint("ck_transfer_limit_policies_night_attempts_nonnegative", "\"NightMaximumAttempts\" >= 0");
                    table.ForeignKey(
                        name: "FK_transfer_limit_policies_account_holders_AccountHolderId",
                        column: x => x.AccountHolderId,
                        principalTable: "account_holders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_transfer_limit_policies_AccountHolderId",
                table: "transfer_limit_policies",
                column: "AccountHolderId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "transfer_limit_policies");
        }
    }
}
