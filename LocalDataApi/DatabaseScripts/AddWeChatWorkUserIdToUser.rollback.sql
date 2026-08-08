-- 回滚脚本:移除企业微信 UserId 绑定列(对应 AddWeChatWorkUserIdToUser.sql)

IF COL_LENGTH('[用户管理]', 'WeChatWorkUserId') IS NOT NULL
BEGIN
    DROP INDEX IF EXISTS [IX_用户管理_WeChatWorkUserId] ON [用户管理];
    ALTER TABLE [用户管理] DROP COLUMN [WeChatWorkUserId];
END
GO
