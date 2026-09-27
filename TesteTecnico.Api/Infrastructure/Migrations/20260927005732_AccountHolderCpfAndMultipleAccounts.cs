using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TesteTecnico.Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AccountHolderCpfAndMultipleAccounts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_accounts_OwnerId",
                table: "accounts");

            migrationBuilder.AddColumn<string>(
                name: "Cpf",
                table: "account_holders",
                type: "character varying(11)",
                maxLength: 11,
                nullable: true);

            migrationBuilder.Sql("""
                WITH ranked AS (
                    SELECT "Id", lpad(row_number() OVER (ORDER BY "Id")::text, 9, '0') AS base
                    FROM "account_holders"
                    WHERE "Cpf" IS NULL
                ), first_digit AS (
                    SELECT ranked."Id", ranked.base,
                        CASE WHEN (sum(substring(ranked.base FROM digits.position_index FOR 1)::integer * (11 - digits.position_index)) * 10 % 11) = 10
                            THEN 0
                            ELSE (sum(substring(ranked.base FROM digits.position_index FOR 1)::integer * (11 - digits.position_index)) * 10 % 11)
                        END AS digit
                    FROM ranked
                    CROSS JOIN generate_series(1, 9) AS digits(position_index)
                    GROUP BY ranked."Id", ranked.base
                ), second_digit AS (
                    SELECT first_digit."Id", first_digit.base, first_digit.digit AS first_check_digit,
                        CASE WHEN (sum(substring(first_digit.base || first_digit.digit::text FROM digits.position_index FOR 1)::integer * (12 - digits.position_index)) * 10 % 11) = 10
                            THEN 0
                            ELSE (sum(substring(first_digit.base || first_digit.digit::text FROM digits.position_index FOR 1)::integer * (12 - digits.position_index)) * 10 % 11)
                        END AS digit
                    FROM first_digit
                    CROSS JOIN generate_series(1, 10) AS digits(position_index)
                    GROUP BY first_digit."Id", first_digit.base, first_digit.digit
                )
                UPDATE "account_holders" AS holder
                SET "Cpf" = second_digit.base || second_digit.first_check_digit::text || second_digit.digit::text
                FROM second_digit
                WHERE holder."Id" = second_digit."Id";
                """);

            migrationBuilder.AlterColumn<string>(
                name: "Cpf",
                table: "account_holders",
                type: "character varying(11)",
                maxLength: 11,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(11)",
                oldMaxLength: 11,
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_accounts_OwnerId",
                table: "accounts",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_account_holders_Cpf",
                table: "account_holders",
                column: "Cpf",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    IF EXISTS (SELECT 1 FROM accounts GROUP BY "OwnerId" HAVING count(*) > 1) THEN
                        RAISE EXCEPTION 'Cannot restore unique account ownership while a holder has multiple accounts.';
                    END IF;
                END $$;
                """);

            migrationBuilder.DropIndex(
                name: "IX_accounts_OwnerId",
                table: "accounts");

            migrationBuilder.DropIndex(
                name: "IX_account_holders_Cpf",
                table: "account_holders");

            migrationBuilder.DropColumn(
                name: "Cpf",
                table: "account_holders");

            migrationBuilder.CreateIndex(
                name: "IX_accounts_OwnerId",
                table: "accounts",
                column: "OwnerId",
                unique: true);
        }
    }
}
