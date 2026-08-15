using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LocalDataApi.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class DropLegacyUserIdentityIdCounter : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Superseded by dbo.Sys_User.Id IDENTITY during the numeric-key cutover.
            // Conditional execution keeps the migration safe if an operator removed
            // the obsolete counter during an earlier maintenance action.
            migrationBuilder.Sql("""
                IF OBJECT_ID(N'dbo.UserIdentityIdCounter', N'U') IS NOT NULL
                    DROP TABLE dbo.UserIdentityIdCounter;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            throw new NotSupportedException("The legacy identity counter belongs to the retired string-key account model and must not be restored.");
        }
    }
}
