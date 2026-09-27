using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TesteTecnico.Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class TransferLimitPoliciesBelongToAccounts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_transfer_limit_policies_account_holders_AccountHolderId",
                table: "transfer_limit_policies");

            migrationBuilder.DropIndex(
                name: "IX_transfer_limit_policies_AccountHolderId",
                table: "transfer_limit_policies");

            migrationBuilder.RenameColumn(
                name: "AccountHolderId",
                table: "transfer_limit_policies",
                newName: "LegacyAccountHolderId");

            migrationBuilder.AddColumn<Guid>(
                name: "AccountId",
                table: "transfer_limit_policies",
                type: "uuid",
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE transfer_limit_policies AS policy
                SET "AccountId" = account."Id"
                FROM accounts AS account
                WHERE account."OwnerId" = policy."LegacyAccountHolderId";
                """);

            migrationBuilder.AlterColumn<Guid>(
                name: "AccountId",
                table: "transfer_limit_policies",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.DropColumn(
                name: "LegacyAccountHolderId",
                table: "transfer_limit_policies");

            migrationBuilder.CreateIndex(
                name: "IX_transfer_limit_policies_AccountId",
                table: "transfer_limit_policies",
                column: "AccountId",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_transfer_limit_policies_accounts_AccountId",
                table: "transfer_limit_policies",
                column: "AccountId",
                principalTable: "accounts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_transfer_limit_policies_accounts_AccountId",
                table: "transfer_limit_policies");

            migrationBuilder.DropIndex(
                name: "IX_transfer_limit_policies_AccountId",
                table: "transfer_limit_policies");

            migrationBuilder.AddColumn<Guid>(
                name: "LegacyAccountHolderId",
                table: "transfer_limit_policies",
                type: "uuid",
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE transfer_limit_policies AS policy
                SET "LegacyAccountHolderId" = account."OwnerId"
                FROM accounts AS account
                WHERE account."Id" = policy."AccountId";
                """);

            migrationBuilder.AlterColumn<Guid>(
                name: "LegacyAccountHolderId",
                table: "transfer_limit_policies",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.DropColumn(
                name: "AccountId",
                table: "transfer_limit_policies");

            migrationBuilder.RenameColumn(
                name: "LegacyAccountHolderId",
                table: "transfer_limit_policies",
                newName: "AccountHolderId");

            migrationBuilder.CreateIndex(
                name: "IX_transfer_limit_policies_AccountHolderId",
                table: "transfer_limit_policies",
                column: "AccountHolderId",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_transfer_limit_policies_account_holders_AccountHolderId",
                table: "transfer_limit_policies",
                column: "AccountHolderId",
                principalTable: "account_holders",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
