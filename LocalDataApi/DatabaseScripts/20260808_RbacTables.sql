-- ============================================================
-- RBAC 权限中心建表脚本(2026-08-08)
-- 说明:本项目数据库为 DB-First,新表/新列通过本脚本落地(不使用 EF Migration)。
-- 幂等:可重复执行;重复执行不会产生重复数据或报错。
-- 表名/列名与 EF 模型(AppDbContext)保持一致:
--   Department / Role / Permission / UserRole / RolePermission / AuditLog
--   用户表 [用户管理] 新增 RBAC 扩展列。
-- 注意:ALTER 与 CREATE INDEX 必须分属不同批处理(GO 分隔)。
-- ============================================================

SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

-- ---------- 1. Department(组织部门) ----------
IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[Department]') AND type = N'U')
BEGIN
    CREATE TABLE [dbo].[Department] (
        [Id]               UNIQUEIDENTIFIER NOT NULL CONSTRAINT [PK_Department] PRIMARY KEY,
        [CorpDepartmentId] NVARCHAR(64)     NOT NULL,
        [Name]             NVARCHAR(128)    NOT NULL,
        [ParentId]         UNIQUEIDENTIFIER NULL,
        [Path]             NVARCHAR(512)    NULL,
        [LeaderUserId]     NVARCHAR(64)     NULL,
        [IsActive]         BIT              NOT NULL CONSTRAINT [DF_Department_IsActive] DEFAULT(1),
        [CreateTime]       DATETIME         NOT NULL CONSTRAINT [DF_Department_CreateTime] DEFAULT(GETDATE()),
        [ModifyTime]       DATETIME         NOT NULL CONSTRAINT [DF_Department_ModifyTime] DEFAULT(GETDATE())
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Department_CorpDepartmentId' AND object_id = OBJECT_ID(N'[dbo].[Department]'))
    CREATE UNIQUE INDEX [IX_Department_CorpDepartmentId] ON [dbo].[Department]([CorpDepartmentId]);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Department_ParentId' AND object_id = OBJECT_ID(N'[dbo].[Department]'))
    CREATE INDEX [IX_Department_ParentId] ON [dbo].[Department]([ParentId]);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Department_Path' AND object_id = OBJECT_ID(N'[dbo].[Department]'))
    CREATE INDEX [IX_Department_Path] ON [dbo].[Department]([Path]);
GO

-- ---------- 2. Role(系统角色) ----------
IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[Role]') AND type = N'U')
BEGIN
    CREATE TABLE [dbo].[Role] (
        [Id]          UNIQUEIDENTIFIER NOT NULL CONSTRAINT [PK_Role] PRIMARY KEY,
        [Code]        NVARCHAR(64)     NOT NULL,
        [Name]        NVARCHAR(128)    NOT NULL,
        [DisplayName] NVARCHAR(128)    NOT NULL,
        [Description] NVARCHAR(512)    NULL,
        [IsBuiltIn]   BIT              NOT NULL CONSTRAINT [DF_Role_IsBuiltIn] DEFAULT(0),
        [IsSystem]    BIT              NOT NULL CONSTRAINT [DF_Role_IsSystem] DEFAULT(0),
        [Enabled]     BIT              NOT NULL CONSTRAINT [DF_Role_Enabled] DEFAULT(1),
        [CreateTime]  DATETIME         NOT NULL CONSTRAINT [DF_Role_CreateTime] DEFAULT(GETDATE()),
        [ModifyTime]  DATETIME         NOT NULL CONSTRAINT [DF_Role_ModifyTime] DEFAULT(GETDATE())
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Role_Code' AND object_id = OBJECT_ID(N'[dbo].[Role]'))
    CREATE UNIQUE INDEX [IX_Role_Code] ON [dbo].[Role]([Code]);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Role_Name' AND object_id = OBJECT_ID(N'[dbo].[Role]'))
    CREATE UNIQUE INDEX [IX_Role_Name] ON [dbo].[Role]([Name]);
GO

-- ---------- 3. Permission(权限点) ----------
IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[Permission]') AND type = N'U')
BEGIN
    CREATE TABLE [dbo].[Permission] (
        [Id]          UNIQUEIDENTIFIER NOT NULL CONSTRAINT [PK_Permission] PRIMARY KEY,
        [Code]        NVARCHAR(128)    NOT NULL,
        [Module]      NVARCHAR(64)     NOT NULL,
        [Resource]    NVARCHAR(64)     NOT NULL,
        [Action]      NVARCHAR(64)     NOT NULL,
        [DisplayName] NVARCHAR(128)    NOT NULL,
        [Description] NVARCHAR(512)    NULL,
        [Enabled]     BIT              NOT NULL CONSTRAINT [DF_Permission_Enabled] DEFAULT(1),
        [CreateTime]  DATETIME         NOT NULL CONSTRAINT [DF_Permission_CreateTime] DEFAULT(GETDATE()),
        [ModifyTime]  DATETIME         NOT NULL CONSTRAINT [DF_Permission_ModifyTime] DEFAULT(GETDATE())
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Permission_Code' AND object_id = OBJECT_ID(N'[dbo].[Permission]'))
    CREATE UNIQUE INDEX [IX_Permission_Code] ON [dbo].[Permission]([Code]);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Permission_ModuleResource' AND object_id = OBJECT_ID(N'[dbo].[Permission]'))
    CREATE INDEX [IX_Permission_ModuleResource] ON [dbo].[Permission]([Module], [Resource]);
GO

-- ---------- 4. UserRole(用户-角色关联) ----------
IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[UserRole]') AND type = N'U')
BEGIN
    CREATE TABLE [dbo].[UserRole] (
        [Id]         UNIQUEIDENTIFIER NOT NULL CONSTRAINT [PK_UserRole] PRIMARY KEY,
        [UserId]     NVARCHAR(64)     NOT NULL,
        [RoleId]     UNIQUEIDENTIFIER NOT NULL,
        [AssignedAt] DATETIME         NOT NULL CONSTRAINT [DF_UserRole_AssignedAt] DEFAULT(GETDATE()),
        [AssignedBy] NVARCHAR(64)     NULL,
        [IsActive]   BIT              NOT NULL CONSTRAINT [DF_UserRole_IsActive] DEFAULT(1),
        [RevokedAt]  DATETIME         NULL
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_UserRole_UserIdRoleId' AND object_id = OBJECT_ID(N'[dbo].[UserRole]'))
    CREATE UNIQUE INDEX [IX_UserRole_UserIdRoleId] ON [dbo].[UserRole]([UserId], [RoleId]);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_UserRole_RoleId' AND object_id = OBJECT_ID(N'[dbo].[UserRole]'))
    CREATE INDEX [IX_UserRole_RoleId] ON [dbo].[UserRole]([RoleId]);
GO

-- ---------- 5. RolePermission(角色-权限关联) ----------
IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[RolePermission]') AND type = N'U')
BEGIN
    CREATE TABLE [dbo].[RolePermission] (
        [Id]           UNIQUEIDENTIFIER NOT NULL CONSTRAINT [PK_RolePermission] PRIMARY KEY,
        [RoleId]       UNIQUEIDENTIFIER NOT NULL,
        [PermissionId] UNIQUEIDENTIFIER NOT NULL,
        [CreateTime]   DATETIME         NOT NULL CONSTRAINT [DF_RolePermission_CreateTime] DEFAULT(GETDATE())
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_RolePermission_RoleIdPermissionId' AND object_id = OBJECT_ID(N'[dbo].[RolePermission]'))
    CREATE UNIQUE INDEX [IX_RolePermission_RoleIdPermissionId] ON [dbo].[RolePermission]([RoleId], [PermissionId]);
GO

-- ---------- 6. AuditLog(审计日志) ----------
IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[AuditLog]') AND type = N'U')
BEGIN
    CREATE TABLE [dbo].[AuditLog] (
        [Id]         UNIQUEIDENTIFIER NOT NULL CONSTRAINT [PK_AuditLog] PRIMARY KEY,
        [UserId]     NVARCHAR(64)     NULL,
        [Action]     NVARCHAR(64)     NOT NULL,
        [TargetType] NVARCHAR(64)     NOT NULL,
        [TargetId]   NVARCHAR(64)     NULL,
        [Content]    NVARCHAR(MAX)    NULL,
        [CreateTime] DATETIME         NOT NULL CONSTRAINT [DF_AuditLog_CreateTime] DEFAULT(GETDATE())
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_AuditLog_CreateTimeAction' AND object_id = OBJECT_ID(N'[dbo].[AuditLog]'))
    CREATE INDEX [IX_AuditLog_CreateTimeAction] ON [dbo].[AuditLog]([CreateTime], [Action]);
GO

-- ---------- 7. 用户表 [用户管理] 新增 RBAC 扩展列 ----------
IF COL_LENGTH(N'[dbo].[用户管理]', N'PrimaryDepartmentId') IS NULL
    ALTER TABLE [dbo].[用户管理] ADD [PrimaryDepartmentId] UNIQUEIDENTIFIER NULL;
GO

IF COL_LENGTH(N'[dbo].[用户管理]', N'PrimaryDepartmentName') IS NULL
    ALTER TABLE [dbo].[用户管理] ADD [PrimaryDepartmentName] NVARCHAR(256) NULL;
GO

IF COL_LENGTH(N'[dbo].[用户管理]', N'Position') IS NULL
    ALTER TABLE [dbo].[用户管理] ADD [Position] NVARCHAR(128) NULL;
GO

IF COL_LENGTH(N'[dbo].[用户管理]', N'IsLeader') IS NULL
    ALTER TABLE [dbo].[用户管理] ADD [IsLeader] BIT NOT NULL CONSTRAINT [DF_用户管理_IsLeader] DEFAULT(0);
GO

IF COL_LENGTH(N'[dbo].[用户管理]', N'PermissionVersion') IS NULL
    ALTER TABLE [dbo].[用户管理] ADD [PermissionVersion] INT NOT NULL CONSTRAINT [DF_用户管理_PermissionVersion] DEFAULT(0);
GO

PRINT N'RBAC 表结构初始化完成。';
GO
