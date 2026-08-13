using Microsoft.EntityFrameworkCore.Migrations;

namespace TraceMap.Samples.FrameworkMigrations;

public sealed class ArchiveAccountMigration : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(
            name: "archive_status",
            table: "accounts",
            schema: "archive");
        migrationBuilder.CreateIndex(
            name: "ix_accounts_archive_status",
            table: "accounts",
            column: "archive_status",
            schema: "archive");
        migrationBuilder.Sql("SELECT synthetic_protected_value FROM internal_demo");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "ix_accounts_archive_status",
            table: "accounts",
            schema: "archive");
        migrationBuilder.DropColumn(
            name: "archive_status",
            table: "accounts",
            schema: "archive");
    }
}
