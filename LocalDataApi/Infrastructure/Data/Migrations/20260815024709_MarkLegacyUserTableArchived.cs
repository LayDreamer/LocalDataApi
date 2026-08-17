using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LocalDataApi.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class MarkLegacyUserTableArchived : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF OBJECT_ID(N'dbo.[用户管理]', N'U') IS NULL
                    RAISERROR(N'Legacy user archive table dbo.[用户管理] does not exist.', 16, 1);

                IF DATABASE_PRINCIPAL_ID(N'db_SysUserLegacyArchiveReader') IS NULL
                    CREATE ROLE db_SysUserLegacyArchiveReader AUTHORIZATION dbo;

                GRANT SELECT ON dbo.[用户管理] TO db_SysUserLegacyArchiveReader;
                DENY INSERT, UPDATE, DELETE ON dbo.[用户管理] TO db_SysUserLegacyArchiveReader;

                IF NOT EXISTS
                (
                    SELECT 1 FROM sys.extended_properties
                    WHERE major_id = OBJECT_ID(N'dbo.[用户管理]')
                      AND minor_id = 0
                      AND name = N'ArchivePolicy'
                )
                    EXEC sys.sp_addextendedproperty
                        @name = N'ArchivePolicy',
                        @value = N'Read-only archive after Sys_User numeric-key cutover; application runtime must not query or write this table.',
                        @level0type = N'SCHEMA', @level0name = N'dbo',
                        @level1type = N'TABLE', @level1name = N'用户管理';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            throw new NotSupportedException("The legacy user archive policy must not be removed by an automatic downgrade.");
        }
    }
}
