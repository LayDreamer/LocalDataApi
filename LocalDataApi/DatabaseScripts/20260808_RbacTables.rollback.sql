-- ============================================================
-- RBAC 权限中心建表脚本回滚(2026-08-08)
-- 警告:执行本脚本将永久删除 RBAC 相关表与用户表扩展列。
-- 仅在确认可以放弃 RBAC 功能时执行;执行前请先备份数据库。
-- 回滚顺序:先删索引,再删表;用户表扩展列最后删除。
-- ============================================================

SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

-- 删除索引(避免先删表时报错,分批次执行)
IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Department_CorpDepartmentId' AND object_id = OBJECT_ID(N'[dbo].[Department]'))
    DROP INDEX [IX_Department_CorpDepartmentId] ON [dbo].[Department];
GO

IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Department_ParentId' AND object_id = OBJECT_ID(N'[dbo].[Department]'))
    DROP INDEX [IX_Department_ParentId] ON [dbo].[Department];
GO

IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Department_Path' AND object_id = OBJECT_ID(N'[dbo].[Department]'))
    DROP INDEX [IX_Department_Path] ON [dbo].[Department];
GO

IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Role_Code' AND object_id = OBJECT_ID(N'[dbo].[Role]'))
    DROP INDEX [IX_Role_Code] ON [dbo].[Role];
GO

IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Role_Name' AND object_id = OBJECT_ID(N'[dbo].[Role]'))
    DROP INDEX [IX_Role_Name] ON [dbo].[Role];
GO

IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Permission_Code' AND object_id = OBJECT_ID(N'[dbo].[Permission]'))
    DROP INDEX [IX_Permission_Code] ON [dbo].[Permission];
GO

IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Permission_ModuleResource' AND object_id = OBJECT_ID(N'[dbo].[Permission]'))
    DROP INDEX [IX_Permission_ModuleResource] ON [dbo].[Permission];
GO

IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_UserRole_UserIdRoleId' AND object_id = OBJECT_ID(N'[dbo].[UserRole]'))
    DROP INDEX [IX_UserRole_UserIdRoleId] ON [dbo].[UserRole];
GO

IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_UserRole_RoleId' AND object_id = OBJECT_ID(N'[dbo].[UserRole]'))
    DROP INDEX [IX_UserRole_RoleId] ON [dbo].[UserRole];
GO

IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_RolePermission_RoleIdPermissionId' AND object_id = OBJECT_ID(N'[dbo].[RolePermission]'))
    DROP INDEX [IX_RolePermission_RoleIdPermissionId] ON [dbo].[RolePermission];
GO

IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_AuditLog_CreateTimeAction' AND object_id = OBJECT_ID(N'[dbo].[AuditLog]'))
    DROP INDEX [IX_AuditLog_CreateTimeAction] ON [dbo].[AuditLog];
GO

-- 删除约束(默认值约束)
DECLARE @sql NVARCHAR(4000);
DECLARE @table NVARCHAR(128);
DECLARE @col NVARCHAR(128);
DECLARE cur CURSOR LOCAL FAST_FORWARD FOR
    SELECT t.name, c.name
    FROM sys.columns c
    JOIN sys.tables t ON t.object_id = c.object_id
    WHERE t.name IN (N'Department', N'Role', N'Permission', N'UserRole', N'RolePermission', N'AuditLog', N'用户管理')
      AND c.name IN (N'IsActive', N'IsBuiltIn', N'IsSystem', N'Enabled', N'CreateTime', N'ModifyTime', N'AssignedAt', N'RevokedAt', N'IsLeader', N'PermissionVersion')
      AND c.default_object_id <> 0;
OPEN cur;
FETCH NEXT FROM cur INTO @table, @col;
WHILE @@FETCH_STATUS = 0
BEGIN
    SET @sql = N'ALTER TABLE [dbo].[' + @table + N'] DROP CONSTRAINT [DF_' + @table + N'_' + @col + N'];';
    BEGIN TRY
        EXEC sp_executesql @sql;
    END TRY
    BEGIN CATCH
        -- 约束名不匹配时忽略(脚本是幂等回滚,尽力而为)
    END CATCH
    FETCH NEXT FROM cur INTO @table, @col;
END
CLOSE cur;
DEALLOCATE cur;
GO

-- 删除表
IF OBJECT_ID(N'[dbo].[RolePermission]', N'U') IS NOT NULL DROP TABLE [dbo].[RolePermission];
IF OBJECT_ID(N'[dbo].[UserRole]', N'U') IS NOT NULL DROP TABLE [dbo].[UserRole];
IF OBJECT_ID(N'[dbo].[AuditLog]', N'U') IS NOT NULL DROP TABLE [dbo].[AuditLog];
IF OBJECT_ID(N'[dbo].[Permission]', N'U') IS NOT NULL DROP TABLE [dbo].[Permission];
IF OBJECT_ID(N'[dbo].[Role]', N'U') IS NOT NULL DROP TABLE [dbo].[Role];
IF OBJECT_ID(N'[dbo].[Department]', N'U') IS NOT NULL DROP TABLE [dbo].[Department];
GO

-- 用户表删除扩展列
IF COL_LENGTH(N'[dbo].[用户管理]', N'PrimaryDepartmentId') IS NOT NULL
    ALTER TABLE [dbo].[用户管理] DROP COLUMN [PrimaryDepartmentId];
GO

IF COL_LENGTH(N'[dbo].[用户管理]', N'PrimaryDepartmentName') IS NOT NULL
    ALTER TABLE [dbo].[用户管理] DROP COLUMN [PrimaryDepartmentName];
GO

IF COL_LENGTH(N'[dbo].[用户管理]', N'Position') IS NOT NULL
    ALTER TABLE [dbo].[用户管理] DROP COLUMN [Position];
GO

IF COL_LENGTH(N'[dbo].[用户管理]', N'IsLeader') IS NOT NULL
    ALTER TABLE [dbo].[用户管理] DROP COLUMN [IsLeader];
GO

IF COL_LENGTH(N'[dbo].[用户管理]', N'PermissionVersion') IS NOT NULL
    ALTER TABLE [dbo].[用户管理] DROP COLUMN [PermissionVersion];
GO

PRINT N'RBAC 表结构已回滚。';
GO
