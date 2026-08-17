/*
  Run once by the DBA after moving LocalDataApi off the sysadmin (sa) login.
  This script deliberately does not create a login or change the connection string:
  its account name, secret storage and required PMC/ERP grants are deployment decisions.

  NOTE: 本脚本与 EF Migration 20260815024709_MarkLegacyUserTableArchived 逻辑重复。
  以 EF 迁移为准;本脚本仅作 DBA 在最小权限切换后的手动兜底(迁移默认不执行 GRANT/DENY 的运行时依赖)。
*/
SET XACT_ABORT ON;
BEGIN TRANSACTION;

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

COMMIT TRANSACTION;

/*
  After creating a least-privilege login, run the following with the real login name:
    CREATE USER [<app-login>] FOR LOGIN [<app-login>];
    ALTER ROLE db_SysUserLegacyArchiveReader ADD MEMBER [<app-login>];
  Do not add the application login to db_owner/sysadmin. Grant its non-identity
  business-table permissions separately and then update ConnectionStrings:DefaultConnection.
*/
