using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LocalDataApi.Infrastructure.Data.Migrations;

/// <summary>
/// Maintenance-window-only cutover from the DB-first legacy user table to the
/// platform-owned numeric Sys_User aggregate.  Every conversion is validated
/// before it is applied; dirty source data causes the transaction to fail.
/// </summary>
public partial class SysUserNumericPrimaryKeyCutover : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            SET XACT_ABORT ON;

            IF OBJECT_ID(N'dbo.[用户管理]', N'U') IS NULL
                RAISERROR(N'Legacy table dbo.[用户管理] does not exist; Sys_User cutover aborted.', 16, 1);

            /* The hand-created table is deliberately adopted only when empty and unused. */
            IF OBJECT_ID(N'dbo.Sys_User', N'U') IS NOT NULL
            BEGIN
                IF EXISTS (SELECT 1 FROM dbo.Sys_User)
                    RAISERROR(N'dbo.Sys_User contains data; cutover refuses to overwrite it.', 16, 1);
                IF EXISTS
                (
                    SELECT 1
                    FROM sys.foreign_keys
                    WHERE referenced_object_id = OBJECT_ID(N'dbo.Sys_User')
                       OR parent_object_id = OBJECT_ID(N'dbo.Sys_User')
                )
                    RAISERROR(N'dbo.Sys_User has foreign-key dependencies; cutover refuses to replace it.', 16, 1);
                DROP TABLE dbo.Sys_User;
            END;

            /* Source data quality gates: nothing is silently renamed, truncated, or defaulted. */
            IF EXISTS
            (
                SELECT IdentityId
                FROM dbo.[用户管理]
                GROUP BY IdentityId
                HAVING IdentityId IS NULL OR COUNT(*) <> 1
            )
                RAISERROR(N'Legacy IdentityId contains NULL or duplicate values; cutover aborted.', 16, 1);

            IF EXISTS
            (
                SELECT 1
                FROM dbo.[用户管理]
                WHERE Id IS NULL OR LEN(Id) > 450
                   OR UserName IS NULL OR LEN(UserName) = 0 OR LEN(UserName) > 128
                   OR DisplayName IS NULL OR LEN(DisplayName) = 0 OR LEN(DisplayName) > 100
                   OR (Email IS NOT NULL AND LEN(Email) > 256)
                   OR (PhoneNumber IS NOT NULL AND LEN(PhoneNumber) > 32)
                   OR (PasswordHash IS NOT NULL AND LEN(PasswordHash) > 512)
                   OR (PasswordSalt IS NOT NULL AND LEN(PasswordSalt) > 256)
                   OR (LastLoginIp IS NOT NULL AND LEN(LastLoginIp) > 64)
                   OR CreateDate IS NULL OR LTRIM(RTRIM(CreateDate)) = N'' OR ISDATE(CreateDate) = 0
                   OR ModifyDate IS NULL OR LTRIM(RTRIM(ModifyDate)) = N'' OR ISDATE(ModifyDate) = 0
                   OR (LastLoginTime IS NOT NULL AND LTRIM(RTRIM(LastLoginTime)) <> N'' AND ISDATE(LastLoginTime) = 0)
                   OR (LockoutEnd IS NOT NULL AND LTRIM(RTRIM(LockoutEnd)) <> N'' AND ISDATE(LockoutEnd) = 0)
                   OR (LoginFailCount IS NOT NULL AND LTRIM(RTRIM(LoginFailCount)) <> N''
                       AND (LoginFailCount LIKE N'%[^0-9]%' OR LEN(LoginFailCount) > 10
                            OR (LEN(LoginFailCount) = 10 AND LoginFailCount > N'2147483647')))
                   OR (WeChatWorkUserId IS NOT NULL AND LEN(WeChatWorkUserId) > 128)
            )
                RAISERROR(N'Legacy user data exceeds Sys_User limits or contains invalid date/failure-count values.', 16, 1);

            IF EXISTS
            (
                SELECT UPPER(UserName)
                FROM dbo.[用户管理]
                GROUP BY UPPER(UserName)
                HAVING COUNT(*) > 1
            )
                RAISERROR(N'Normalized legacy usernames are duplicated; cutover aborted.', 16, 1);

            IF EXISTS
            (
                SELECT WeChatWorkUserId
                FROM dbo.[用户管理]
                WHERE WeChatWorkUserId IS NOT NULL AND LTRIM(RTRIM(WeChatWorkUserId)) <> N''
                GROUP BY WeChatWorkUserId
                HAVING COUNT(*) > 1
            )
                RAISERROR(N'Duplicate WeChatWork user identifiers found; cutover aborted.', 16, 1);

            CREATE TABLE dbo.Sys_User
            (
                Id bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_Sys_User PRIMARY KEY,
                UserName nvarchar(128) NOT NULL,
                NormalizedUserName nvarchar(128) NOT NULL,
                DisplayName nvarchar(100) NOT NULL,
                Email nvarchar(256) NULL,
                PhoneNumber nvarchar(32) NULL,
                Status tinyint NOT NULL CONSTRAINT DF_Sys_User_Status DEFAULT (1),
                PasswordHash nvarchar(512) NULL,
                PasswordSalt nvarchar(256) NULL,
                PasswordAlgorithm nvarchar(32) NULL,
                PasswordUpdatedAtUtc datetime2 NULL,
                MustChangePassword bit NOT NULL CONSTRAINT DF_Sys_User_MustChangePassword DEFAULT (0),
                LoginFailCount int NOT NULL CONSTRAINT DF_Sys_User_LoginFailCount DEFAULT (0),
                LockoutEndUtc datetime2 NULL,
                LastLoginAtUtc datetime2 NULL,
                LastLoginIp nvarchar(64) NULL,
                PermissionVersion int NOT NULL CONSTRAINT DF_Sys_User_PermissionVersion DEFAULT (0),
                CreatedAtUtc datetime2 NOT NULL,
                UpdatedAtUtc datetime2 NOT NULL,
                RowVersion rowversion NOT NULL
            );
            CREATE UNIQUE INDEX UX_Sys_User_NormalizedUserName ON dbo.Sys_User(NormalizedUserName);
            CREATE INDEX IX_Sys_User_Status ON dbo.Sys_User(Status);

            /* Legacy date strings were written by the China Standard Time application server. */
            DECLARE @legacyUtcOffsetHours int = -8;
            SET IDENTITY_INSERT dbo.Sys_User ON;
            INSERT dbo.Sys_User
            (
                Id, UserName, NormalizedUserName, DisplayName, Email, PhoneNumber, Status,
                PasswordHash, PasswordSalt, PasswordAlgorithm, PasswordUpdatedAtUtc,
                MustChangePassword, LoginFailCount, LockoutEndUtc, LastLoginAtUtc, LastLoginIp,
                PermissionVersion, CreatedAtUtc, UpdatedAtUtc
            )
            SELECT
                IdentityId, UserName, UPPER(UserName), DisplayName, Email, PhoneNumber,
                CASE WHEN LOWER(LTRIM(RTRIM(ISNULL(IsActive, N'')))) = N'true' THEN 1 ELSE 2 END,
                PasswordHash, PasswordSalt,
                CASE WHEN PasswordHash IS NULL THEN NULL ELSE N'PBKDF2-SHA256-100000' END,
                CASE WHEN PasswordHash IS NULL THEN NULL ELSE DATEADD(HOUR, @legacyUtcOffsetHours, CONVERT(datetime2, ModifyDate)) END,
                MustChangePassword,
                CASE WHEN LoginFailCount IS NULL OR LTRIM(RTRIM(LoginFailCount)) = N'' THEN 0 ELSE CONVERT(int, LoginFailCount) END,
                CASE WHEN LockoutEnd IS NULL OR LTRIM(RTRIM(LockoutEnd)) = N'' THEN NULL ELSE DATEADD(HOUR, @legacyUtcOffsetHours, CONVERT(datetime2, LockoutEnd)) END,
                CASE WHEN LastLoginTime IS NULL OR LTRIM(RTRIM(LastLoginTime)) = N'' THEN NULL ELSE DATEADD(HOUR, @legacyUtcOffsetHours, CONVERT(datetime2, LastLoginTime)) END,
                LastLoginIp, PermissionVersion,
                DATEADD(HOUR, @legacyUtcOffsetHours, CONVERT(datetime2, CreateDate)),
                DATEADD(HOUR, @legacyUtcOffsetHours, CONVERT(datetime2, ModifyDate))
            FROM dbo.[用户管理];
            SET IDENTITY_INSERT dbo.Sys_User OFF;

            CREATE TABLE dbo.Sys_UserLegacyMap
            (
                LegacyUserId nvarchar(450) NOT NULL CONSTRAINT PK_Sys_UserLegacyMap PRIMARY KEY,
                UserId bigint NOT NULL,
                MigratedAtUtc datetime2 NOT NULL,
                CONSTRAINT UX_Sys_UserLegacyMap_UserId UNIQUE(UserId),
                CONSTRAINT FK_Sys_UserLegacyMap_Sys_User_UserId FOREIGN KEY(UserId)
                    REFERENCES dbo.Sys_User(Id)
            );
            INSERT dbo.Sys_UserLegacyMap(LegacyUserId, UserId, MigratedAtUtc)
            SELECT Id, IdentityId, SYSUTCDATETIME() FROM dbo.[用户管理];

            CREATE TABLE dbo.Sys_UserExternalIdentity
            (
                Id bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_Sys_UserExternalIdentity PRIMARY KEY,
                UserId bigint NOT NULL,
                Provider nvarchar(32) NOT NULL,
                ExternalSubject nvarchar(128) NOT NULL,
                CreatedAtUtc datetime2 NOT NULL,
                CONSTRAINT FK_Sys_UserExternalIdentity_Sys_User_UserId FOREIGN KEY(UserId)
                    REFERENCES dbo.Sys_User(Id)
            );
            CREATE UNIQUE INDEX UX_Sys_UserExternalIdentity_Provider_Subject
                ON dbo.Sys_UserExternalIdentity(Provider, ExternalSubject);
            CREATE INDEX IX_Sys_UserExternalIdentity_UserId ON dbo.Sys_UserExternalIdentity(UserId);
            INSERT dbo.Sys_UserExternalIdentity(UserId, Provider, ExternalSubject, CreatedAtUtc)
            SELECT IdentityId, N'WeChatWork', WeChatWorkUserId, SYSUTCDATETIME()
            FROM dbo.[用户管理]
            WHERE WeChatWorkUserId IS NOT NULL AND LTRIM(RTRIM(WeChatWorkUserId)) <> N'';

            DECLARE @maxUserId bigint = (SELECT ISNULL(MAX(Id), 0) FROM dbo.Sys_User);
            DECLARE @reseedSql nvarchar(300) = N'DBCC CHECKIDENT (N''dbo.Sys_User'', RESEED, '
                + CONVERT(nvarchar(30), @maxUserId) + N') WITH NO_INFOMSGS;';
            EXEC(@reseedSql);
            """);

        migrationBuilder.Sql("""
            /* UserRole: map before dropping the legacy string key. */
            /* An orphaned legacy role cannot satisfy the new non-null FK.  Preserve
               the revocation as an audit event rather than inventing an account. */
            INSERT dbo.AuditLog(Id, UserId, Action, TargetType, TargetId, Content, CreateTime)
            SELECT NEWID(), ur.UserId, N'UserRole.RevokedForUserIdCutover', N'UserRole',
                   CONVERT(nvarchar(450), ur.Id),
                   N'{"reason":"legacy-user-not-found-during-user-id-cutover"}', GETDATE()
            FROM UserRole ur
            LEFT JOIN dbo.Sys_UserLegacyMap m ON m.LegacyUserId = ur.UserId
            WHERE m.UserId IS NULL;
            DELETE ur
            FROM UserRole ur
            LEFT JOIN dbo.Sys_UserLegacyMap m ON m.LegacyUserId = ur.UserId
            WHERE m.UserId IS NULL;
            IF EXISTS
            (
                SELECT 1 FROM UserRole ur
                LEFT JOIN dbo.Sys_UserLegacyMap m ON m.LegacyUserId = ur.AssignedBy
                WHERE ur.AssignedBy IS NOT NULL AND LTRIM(RTRIM(ur.AssignedBy)) <> N'' AND m.UserId IS NULL
            )
                RAISERROR(N'UserRole.AssignedBy contains a user that cannot be mapped to Sys_User.', 16, 1);
            ALTER TABLE UserRole ADD PlatformUserId bigint NULL, PlatformAssignedBy bigint NULL;
            """);

        migrationBuilder.Sql("""
            UPDATE ur SET PlatformUserId = m.UserId FROM UserRole ur JOIN dbo.Sys_UserLegacyMap m ON m.LegacyUserId = ur.UserId;
            UPDATE ur SET PlatformAssignedBy = m.UserId FROM UserRole ur JOIN dbo.Sys_UserLegacyMap m ON m.LegacyUserId = ur.AssignedBy;
            """);

        migrationBuilder.Sql("""
            DROP INDEX IX_UserRole_UserId_RoleId ON UserRole;
            ALTER TABLE UserRole DROP COLUMN UserId, AssignedBy;
            """);

        migrationBuilder.Sql("""
            EXEC sp_rename N'UserRole.PlatformUserId', N'UserId', N'COLUMN';
            EXEC sp_rename N'UserRole.PlatformAssignedBy', N'AssignedBy', N'COLUMN';
            """);

        migrationBuilder.Sql("""
            ALTER TABLE UserRole ALTER COLUMN UserId bigint NOT NULL;
            CREATE UNIQUE INDEX IX_UserRole_UserId_RoleId ON UserRole(UserId, RoleId);
            ALTER TABLE UserRole ADD CONSTRAINT FK_UserRole_Sys_User_UserId FOREIGN KEY(UserId) REFERENCES dbo.Sys_User(Id);
            """);

        migrationBuilder.Sql("""
            /* Existing sessions cannot safely survive a subject-key replacement. */
            IF EXISTS
            (
                SELECT 1 FROM AuthSession s
                LEFT JOIN dbo.Sys_UserLegacyMap m ON m.LegacyUserId = s.UserId
                WHERE m.UserId IS NULL
            )
                RAISERROR(N'AuthSession contains a user that cannot be mapped to Sys_User.', 16, 1);
            ALTER TABLE AuthSession ADD PlatformUserId bigint NULL;
            """);

        migrationBuilder.Sql("""
            UPDATE s SET PlatformUserId = m.UserId FROM AuthSession s JOIN dbo.Sys_UserLegacyMap m ON m.LegacyUserId = s.UserId;
            UPDATE AuthSession SET RevokedAtUtc = SYSUTCDATETIME(), RevokedReason = N'user-id-cutover' WHERE RevokedAtUtc IS NULL;
            """);

        migrationBuilder.Sql("""
            DROP INDEX IX_AuthSession_UserId ON AuthSession;
            ALTER TABLE AuthSession DROP COLUMN UserId;
            """);

        migrationBuilder.Sql("""
            EXEC sp_rename N'AuthSession.PlatformUserId', N'UserId', N'COLUMN';
            """);

        migrationBuilder.Sql("""
            ALTER TABLE AuthSession ALTER COLUMN UserId bigint NOT NULL;
            CREATE INDEX IX_AuthSession_UserId ON AuthSession(UserId);
            ALTER TABLE AuthSession ADD CONSTRAINT FK_AuthSession_Sys_User_UserId FOREIGN KEY(UserId) REFERENCES dbo.Sys_User(Id);
            """);

        migrationBuilder.Sql("""
            /* Employee preserves its already-numeric IdentityId values and changes only its target. */
            ALTER TABLE dbo.Employee DROP CONSTRAINT FK_Employee_UserIdentity;
            DROP INDEX UX_Employee_UserIdentityId ON dbo.Employee;
            EXEC sp_rename N'dbo.Employee.UserIdentityId', N'UserId', N'COLUMN';
            """);

        migrationBuilder.Sql("""
            CREATE UNIQUE INDEX UX_Employee_UserId ON dbo.Employee(UserId) WHERE UserId IS NOT NULL;
            ALTER TABLE dbo.Employee ADD CONSTRAINT FK_Employee_SysUser FOREIGN KEY(UserId) REFERENCES dbo.Sys_User(Id);
            """);

        migrationBuilder.Sql("""
            /* The source WeChat leader text remains as a synchronization/archive value. */
            ALTER TABLE Department ADD LeaderExternalUserId nvarchar(max) NULL, LeaderPlatformUserId bigint NULL;
            """);

        migrationBuilder.Sql("""
            UPDATE Department SET LeaderExternalUserId = LeaderUserId;
            UPDATE d SET LeaderPlatformUserId = x.UserId
            FROM Department d
            JOIN dbo.Sys_UserExternalIdentity x
              ON x.Provider = N'WeChatWork' AND x.ExternalSubject = d.LeaderExternalUserId;
            """);

        migrationBuilder.Sql("""
            ALTER TABLE Department DROP COLUMN LeaderUserId;
            """);

        migrationBuilder.Sql("""
            EXEC sp_rename N'Department.LeaderPlatformUserId', N'LeaderUserId', N'COLUMN';
            """);

        migrationBuilder.Sql("""
            CREATE INDEX IX_Department_LeaderUserId ON Department(LeaderUserId);
            ALTER TABLE Department ADD CONSTRAINT FK_Department_Sys_User_LeaderUserId
                FOREIGN KEY(LeaderUserId) REFERENCES dbo.Sys_User(Id);
            """);

        migrationBuilder.Sql("""
            /* Historical actor strings are retained; numeric identifiers are additive and nullable. */
            ALTER TABLE AuditLog ADD PlatformUserId bigint NULL;
            ALTER TABLE LoginLog ADD PlatformUserId bigint NULL;
            ALTER TABLE OperationLog ADD PlatformUserId bigint NULL;
            ALTER TABLE DataChangeLog ADD PlatformUserId bigint NULL;
            """);

        migrationBuilder.Sql("""
            UPDATE a SET PlatformUserId = m.UserId FROM AuditLog a JOIN dbo.Sys_UserLegacyMap m ON m.LegacyUserId = a.UserId;
            UPDATE l SET PlatformUserId = m.UserId FROM LoginLog l JOIN dbo.Sys_UserLegacyMap m ON m.LegacyUserId = l.UserId;
            UPDATE o SET PlatformUserId = m.UserId FROM OperationLog o JOIN dbo.Sys_UserLegacyMap m ON m.LegacyUserId = o.UserId;
            UPDATE d SET PlatformUserId = m.UserId FROM DataChangeLog d JOIN dbo.Sys_UserLegacyMap m ON m.LegacyUserId = d.OperatorUserId;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
        => throw new NotSupportedException("Sys_User numeric-key cutover requires the reviewed maintenance-window rollback procedure; automatic downgrade is unsafe.");
}
