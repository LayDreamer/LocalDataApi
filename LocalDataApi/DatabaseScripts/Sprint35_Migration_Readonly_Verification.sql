/* =============================================================================
   永创制造数字化平台 — Sprint3.5 Task05 Migration 只读校验脚本
   =============================================================================
   目标库 : ycdbnnew (SQL Server 2008 兼容)
   用途   : 只读校验 Migration 链与关键表数据一致性,供 DBA 上线前/巡检使用
   执行   : 在目标库以只读权限运行;本脚本仅 SELECT/OBJECT_ID,不写库
   注意   : 表名/字段名以 EF 实体映射为准(见 AppDbContext / Migrations)
   ============================================================================= */

SET NOCOUNT ON;

DECLARE @failCount INT = 0;
DECLARE @sectionHeader NVARCHAR(200);

/* =============================================================================
   1. 迁移历史比对:本地 19 个迁移 ID 与 __EFMigrationsHistory 核对
   ============================================================================= */
SET @sectionHeader = N'=== 1. 迁移历史比对 (__EFMigrationsHistory) ===';
PRINT @sectionHeader;

IF OBJECT_ID(N'dbo.__EFMigrationsHistory', N'U') IS NULL
BEGIN
    PRINT '   [FAIL] __EFMigrationsHistory 表不存在 (数据库未执行 EF Migration)';
    SET @failCount = @failCount + 1;
END
ELSE
BEGIN
    -- 预期迁移 ID 清单(与 Migrations 目录 19 个迁移一一对应)
    CREATE TABLE #ExpectedMigrations (MigrationId NVARCHAR(150) PRIMARY KEY);
    INSERT INTO #ExpectedMigrations (MigrationId) VALUES
        (N'20260120044605_InitialCreate'),
        (N'20260808080622_AddRbacTables'),
        (N'20260810000000_AddUserMustChangePassword'),
        (N'20260811000000_AddAuthSessions'),
        (N'20260813022743_AddAuditCenterLogs'),
        (N'20260813081814_Sprint2_Add_Menu_Model'),
        (N'20260814025749_Sprint3_Add_Position'),
        (N'20260814054246_AddEmployee'),
        (N'20260814055615_AddUserIdentityId'),
        (N'20260814061936_UpgradeUserIdentityIdConstraints'),
        (N'20260814062748_AddEmployeeUserIdentityId'),
        (N'20260814070427_AddUserIdentityIdCounter'),
        (N'20260814075431_AddEmployeeUserIdentityIdUniqueConstraint'),
        (N'20260815014530_SysUserNumericPrimaryKeyCutover'),
        (N'20260815024044_DropLegacyUserIdentityIdCounter'),
        (N'20260815024709_MarkLegacyUserTableArchived'),
        (N'20260815083751_AddDictionaryCenter'),
        (N'20260817071524_AddNumberRule'),
        (N'20260818014041_AddAttachment');

    -- 清单中存在但历史表缺失的迁移
    SELECT '   [FAIL] 缺失迁移: ' + e.MigrationId AS Result
    FROM #ExpectedMigrations e
    LEFT JOIN dbo.__EFMigrationsHistory h ON h.MigrationId = e.MigrationId
    WHERE h.MigrationId IS NULL;

    IF @@ROWCOUNT = 0
        PRINT '   [OK] 19 个预期迁移全部存在';
    ELSE
        SET @failCount = @failCount + 1;

    -- 历史表中存在但本地清单之外的迁移(漂移)
    SELECT '   [INFO] 历史表多余迁移: ' + h.MigrationId AS Result
    FROM dbo.__EFMigrationsHistory h
    LEFT JOIN #ExpectedMigrations e ON e.MigrationId = h.MigrationId
    WHERE e.MigrationId IS NULL;

    DROP TABLE #ExpectedMigrations;
END

/* =============================================================================
   2. 数据对账:Sys_User vs 用户管理 / Sys_UserLegacyMap 映射
   ============================================================================= */
SET @sectionHeader = N'=== 2. 数据对账 (Sys_User / 用户管理 / Sys_UserLegacyMap) ===';
PRINT @sectionHeader;

DECLARE @sysUserCount INT = 0, @legacyUserCount INT = 0, @mapCount INT = 0;

SELECT @sysUserCount = COUNT(*) FROM dbo.Sys_User WHERE 1 = 1;
SELECT @legacyUserCount = COUNT(*) FROM dbo.[用户管理] WHERE 1 = 1;
SELECT @mapCount = COUNT(*) FROM dbo.Sys_UserLegacyMap WHERE 1 = 1;

PRINT '   [INFO] Sys_User 行数 = ' + CAST(@sysUserCount AS NVARCHAR(20))
    + ', 用户管理 行数 = ' + CAST(@legacyUserCount AS NVARCHAR(20))
    + ', Sys_UserLegacyMap 行数 = ' + CAST(@mapCount AS NVARCHAR(20));

-- 映射缺失:用户管理中未在 LegacyMap 找到映射的旧账号(未进入新主表)
SELECT '   [FAIL] 用户管理无映射: ' + CAST(Id AS NVARCHAR(50)) AS Result
FROM dbo.[用户管理] u
WHERE NOT EXISTS (SELECT 1 FROM dbo.Sys_UserLegacyMap m WHERE m.LegacyUserId = u.Id);

IF @@ROWCOUNT = 0
    PRINT '   [OK] 用户管理全部存在 LegacyMap 映射';
ELSE
    SET @failCount = @failCount + 1;

-- NormalizedUserName 唯一性(重复会导致登录歧义)
SELECT '   [FAIL] NormalizedUserName 重复: ' + NormalizedUserName AS Result
FROM dbo.Sys_User
GROUP BY NormalizedUserName
HAVING COUNT(*) > 1;

IF @@ROWCOUNT = 0
    PRINT '   [OK] Sys_User.NormalizedUserName 无重复';
ELSE
    SET @failCount = @failCount + 1;

/* =============================================================================
   3. 外键完整性:UserRole / AuthSession / Employee / Department 孤儿检查
   ============================================================================= */
SET @sectionHeader = N'=== 3. 外键完整性 (孤儿检查) ===';
PRINT @sectionHeader;

SELECT '   [FAIL] UserRole 孤儿 UserId: ' + CAST(ur.UserId AS NVARCHAR(20)) AS Result
FROM dbo.UserRole ur
LEFT JOIN dbo.Sys_User u ON u.Id = ur.UserId
WHERE u.Id IS NULL;

IF @@ROWCOUNT = 0 PRINT '   [OK] UserRole.UserId 无孤儿'; ELSE SET @failCount = @failCount + 1;

SELECT '   [FAIL] AuthSession 孤儿 UserId: ' + CAST(s.UserId AS NVARCHAR(20)) AS Result
FROM dbo.AuthSession s
LEFT JOIN dbo.Sys_User u ON u.Id = s.UserId
WHERE u.Id IS NULL;

IF @@ROWCOUNT = 0 PRINT '   [OK] AuthSession.UserId 无孤儿'; ELSE SET @failCount = @failCount + 1;

SELECT '   [FAIL] Employee 孤儿 UserId: ' + CAST(e.UserId AS NVARCHAR(20)) AS Result
FROM dbo.Employee e
LEFT JOIN dbo.Sys_User u ON u.Id = e.UserId
WHERE e.UserId IS NOT NULL AND u.Id IS NULL;

IF @@ROWCOUNT = 0 PRINT '   [OK] Employee.UserId 无孤儿'; ELSE SET @failCount = @failCount + 1;

SELECT '   [FAIL] Department 孤儿 LeaderUserId: ' + CAST(d.LeaderUserId AS NVARCHAR(20)) AS Result
FROM dbo.Department d
LEFT JOIN dbo.Sys_User u ON u.Id = d.LeaderUserId
WHERE d.LeaderUserId IS NOT NULL AND u.Id IS NULL;

IF @@ROWCOUNT = 0 PRINT '   [OK] Department.LeaderUserId 无孤儿'; ELSE SET @failCount = @failCount + 1;

/* =============================================================================
   4. 四日志回填:PlatformUserId 空值检查(cutover 迁移应已回填)
   ============================================================================= */
SET @sectionHeader = N'=== 4. 四日志 PlatformUserId 回填检查 ===';
PRINT @sectionHeader;

DECLARE @total INT = 0, @nullCount INT = 0;

SELECT @total = COUNT(*), @nullCount = SUM(CASE WHEN PlatformUserId IS NULL THEN 1 ELSE 0 END)
FROM dbo.AuditLog WHERE 1 = 1;
IF @nullCount > 0
BEGIN
    PRINT '   [FAIL] AuditLog PlatformUserId 未回填: ' + CAST(@nullCount AS NVARCHAR(20)) + '/' + CAST(@total AS NVARCHAR(20));
    SET @failCount = @failCount + 1;
END
ELSE
    PRINT '   [OK] AuditLog PlatformUserId 全部回填 (' + CAST(@total AS NVARCHAR(20)) + ' 行)';

SELECT @total = COUNT(*), @nullCount = SUM(CASE WHEN PlatformUserId IS NULL THEN 1 ELSE 0 END)
FROM dbo.LoginLog WHERE 1 = 1;
IF @nullCount > 0
BEGIN
    PRINT '   [FAIL] LoginLog PlatformUserId 未回填: ' + CAST(@nullCount AS NVARCHAR(20)) + '/' + CAST(@total AS NVARCHAR(20));
    SET @failCount = @failCount + 1;
END
ELSE
    PRINT '   [OK] LoginLog PlatformUserId 全部回填 (' + CAST(@total AS NVARCHAR(20)) + ' 行)';

SELECT @total = COUNT(*), @nullCount = SUM(CASE WHEN PlatformUserId IS NULL THEN 1 ELSE 0 END)
FROM dbo.OperationLog WHERE 1 = 1;
IF @nullCount > 0
BEGIN
    PRINT '   [FAIL] OperationLog PlatformUserId 未回填: ' + CAST(@nullCount AS NVARCHAR(20)) + '/' + CAST(@total AS NVARCHAR(20));
    SET @failCount = @failCount + 1;
END
ELSE
    PRINT '   [OK] OperationLog PlatformUserId 全部回填 (' + CAST(@total AS NVARCHAR(20)) + ' 行)';

SELECT @total = COUNT(*), @nullCount = SUM(CASE WHEN PlatformUserId IS NULL THEN 1 ELSE 0 END)
FROM dbo.DataChangeLog WHERE 1 = 1;
IF @nullCount > 0
BEGIN
    PRINT '   [FAIL] DataChangeLog PlatformUserId 未回填: ' + CAST(@nullCount AS NVARCHAR(20)) + '/' + CAST(@total AS NVARCHAR(20));
    SET @failCount = @failCount + 1;
END
ELSE
    PRINT '   [OK] DataChangeLog PlatformUserId 全部回填 (' + CAST(@total AS NVARCHAR(20)) + ' 行)';

/* =============================================================================
   5. 归档检查:用户管理只读角色与 ArchivePolicy 扩展属性
   ============================================================================= */
SET @sectionHeader = N'=== 5. 用户管理归档检查 ===';
PRINT @sectionHeader;

IF DATABASE_PRINCIPAL_ID(N'db_SysUserLegacyArchiveReader') IS NOT NULL
    PRINT '   [OK] 归档角色 db_SysUserLegacyArchiveReader 存在';
ELSE
BEGIN
    PRINT '   [FAIL] 归档角色 db_SysUserLegacyArchiveReader 不存在';
    SET @failCount = @failCount + 1;
END

IF EXISTS (SELECT 1 FROM sys.extended_properties
           WHERE major_id = OBJECT_ID(N'dbo.[用户管理]') AND name = N'ArchivePolicy')
    PRINT '   [OK] 用户管理 ArchivePolicy 扩展属性存在';
ELSE
BEGIN
    PRINT '   [FAIL] 用户管理 ArchivePolicy 扩展属性缺失';
    SET @failCount = @failCount + 1;
END

/* =============================================================================
   6. 白名单结构抽查:关键表类型/可空性核对
   ============================================================================= */
SET @sectionHeader = N'=== 6. 白名单结构抽查 ===';
PRINT @sectionHeader;

IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
           WHERE TABLE_SCHEMA = N'dbo' AND TABLE_NAME = N'Sys_User'
             AND COLUMN_NAME = N'Id' AND DATA_TYPE = N'bigint')
    PRINT '   [OK] Sys_User.Id = bigint';
ELSE
BEGIN
    PRINT '   [FAIL] Sys_User.Id 不是 bigint';
    SET @failCount = @failCount + 1;
END

IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
           WHERE TABLE_SCHEMA = N'dbo' AND TABLE_NAME = N'Employee'
             AND COLUMN_NAME = N'UserId' AND DATA_TYPE = N'bigint' AND IS_NULLABLE = N'YES')
    PRINT '   [OK] Employee.UserId = bigint NULL';
ELSE
BEGIN
    PRINT '   [FAIL] Employee.UserId 类型/可空性不符';
    SET @failCount = @failCount + 1;
END

IF OBJECT_ID(N'dbo.Sys_Menu', N'U') IS NOT NULL
    PRINT '   [OK] Sys_Menu 表存在';
ELSE
BEGIN
    PRINT '   [FAIL] Sys_Menu 表缺失';
    SET @failCount = @failCount + 1;
END

/* =============================================================================
   7. WP04 附件中心结构抽查:Sys_Attachment 表 + 业务索引 (20260818014041_AddAttachment)
   ============================================================================= */
SET @sectionHeader = N'=== 7. WP04 附件中心 (Sys_Attachment) ===';
PRINT @sectionHeader;

IF OBJECT_ID(N'dbo.Sys_Attachment', N'U') IS NOT NULL
    PRINT '   [OK] Sys_Attachment 表存在';
ELSE
BEGIN
    PRINT '   [FAIL] Sys_Attachment 表缺失 (未执行 AddAttachment 迁移)';
    SET @failCount = @failCount + 1;
END

IF EXISTS (SELECT 1 FROM sys.indexes
           WHERE object_id = OBJECT_ID(N'dbo.Sys_Attachment') AND name = N'IX_Sys_Attachment_Business')
    PRINT '   [OK] IX_Sys_Attachment_Business 索引存在';
ELSE
BEGIN
    PRINT '   [FAIL] IX_Sys_Attachment_Business 索引缺失';
    SET @failCount = @failCount + 1;
END

IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
           WHERE TABLE_SCHEMA = N'dbo' AND TABLE_NAME = N'Sys_Attachment'
             AND COLUMN_NAME = N'StorageKey' AND DATA_TYPE = N'nvarchar' AND CHARACTER_MAXIMUM_LENGTH = 512)
    PRINT '   [OK] Sys_Attachment.StorageKey = nvarchar(512)';
ELSE
BEGIN
    PRINT '   [FAIL] Sys_Attachment.StorageKey 类型/长度不符';
    SET @failCount = @failCount + 1;
END

/* =============================================================================
   8. 统一汇总
   ============================================================================= */
PRINT N'';
PRINT N'=== 8. 校验汇总 ===';
PRINT N'   失败项总数 = ' + CAST(@failCount AS NVARCHAR(10));
IF @failCount = 0
    PRINT N'   [PASS] 全部校验通过';
ELSE
    PRINT N'   [FAIL] 存在 ' + CAST(@failCount AS NVARCHAR(10)) + ' 项异常,请逐项核查';
