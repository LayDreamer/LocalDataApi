-- 企业微信工作台免登:为「用户管理」表新增企业微信 UserId 绑定列
-- 执行前请确认 [用户管理] 表名/列名与真实生产库一致(本项目数据库为 DB-First)。
-- 过滤唯一索引确保一个企微身份只绑定一个系统账号,同时允许多个账号该列为 NULL。
-- 注意:ALTER 与 CREATE INDEX 必须分属不同批处理(GO 分隔),否则编译期会因列尚不存在而报错。
-- 筛选索引要求会话启用 QUOTED_IDENTIFIER / ANSI_NULLS。

SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

IF COL_LENGTH('[用户管理]', 'WeChatWorkUserId') IS NULL
BEGIN
    ALTER TABLE [用户管理] ADD [WeChatWorkUserId] NVARCHAR(64) NULL;
END
GO

IF COL_LENGTH('[用户管理]', 'WeChatWorkUserId') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_用户管理_WeChatWorkUserId')
BEGIN
    CREATE UNIQUE INDEX [IX_用户管理_WeChatWorkUserId]
        ON [用户管理]([WeChatWorkUserId])
        WHERE [WeChatWorkUserId] IS NOT NULL;
END
GO
