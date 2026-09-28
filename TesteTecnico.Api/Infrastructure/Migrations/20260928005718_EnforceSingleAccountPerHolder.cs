using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TesteTecnico.Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class EnforceSingleAccountPerHolder : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Reassign only the three known legacy demo accounts. Financial data and
            // account IDs remain unchanged, preserving transfers, policies and Pix keys.
            var demoAccounts = new[]
            {
                (Account: "019942d0-0020-7000-8000-000000000020", Holder: "019942d0-0110-7000-8000-000000000110", Name: "Titular de Teste 11", Cpf: "00000001163"),
                (Account: "019942d0-0021-7000-8000-000000000021", Holder: "019942d0-0111-7000-8000-000000000111", Name: "Titular de Teste 12", Cpf: "00000001244"),
                (Account: "019942d0-0022-7000-8000-000000000022", Holder: "019942d0-0112-7000-8000-000000000112", Name: "Titular de Teste 13", Cpf: "00000001325")
            };

            foreach (var demo in demoAccounts)
            {
                migrationBuilder.Sql($"""
                    INSERT INTO "account_holders" ("Id", "Name", "Cpf")
                    SELECT '{demo.Holder}'::uuid, '{demo.Name}', '{demo.Cpf}'
                    WHERE EXISTS (
                        SELECT 1 FROM "accounts"
                        WHERE "Id" = '{demo.Account}'::uuid
                          AND "OwnerId" = '019942d0-0100-7000-8000-000000000100'::uuid)
                      AND NOT EXISTS (SELECT 1 FROM "account_holders" WHERE "Id" = '{demo.Holder}'::uuid);

                    UPDATE "accounts" SET "OwnerId" = '{demo.Holder}'::uuid
                    WHERE "Id" = '{demo.Account}'::uuid
                      AND "OwnerId" = '019942d0-0100-7000-8000-000000000100'::uuid;
                    """);
            }

            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    IF EXISTS (SELECT "OwnerId" FROM "accounts" GROUP BY "OwnerId" HAVING count(*) > 1) THEN
                        RAISE EXCEPTION 'Existem titulares com mais de uma conta fora do seed conhecido. Corrija os vínculos antes de aplicar EnforceSingleAccountPerHolder.';
                    END IF;
                END $$;
                """);

            migrationBuilder.DropIndex(
                name: "IX_accounts_OwnerId",
                table: "accounts");

            migrationBuilder.CreateIndex(
                name: "IX_accounts_OwnerId",
                table: "accounts",
                column: "OwnerId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Keep demo holders and their associations when relaxing the constraint.
            migrationBuilder.DropIndex(
                name: "IX_accounts_OwnerId",
                table: "accounts");

            migrationBuilder.CreateIndex(
                name: "IX_accounts_OwnerId",
                table: "accounts",
                column: "OwnerId");
        }
    }
}
