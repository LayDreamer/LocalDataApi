/*
  SQL Server 2008 PMC concurrency/performance upgrade.
  Run the duplicate checks first in production. The transaction is rolled back
  automatically when an expected business key contains duplicates.
*/
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET ARITHABORT ON;
SET NUMERIC_ROUNDABORT OFF;
DECLARE @PreviousCompatibilityLevel int;
DECLARE @CompatibilitySql nvarchar(4000);
SELECT @PreviousCompatibilityLevel = compatibility_level
FROM sys.databases
WHERE name = DB_NAME();

IF @PreviousCompatibilityLevel < 100
BEGIN
    SET @CompatibilitySql = N'ALTER DATABASE ' + QUOTENAME(DB_NAME()) + N' SET COMPATIBILITY_LEVEL = 100';
    EXEC(@CompatibilitySql);
END;

BEGIN TRY
BEGIN TRANSACTION;

IF EXISTS (SELECT 1 FROM [生产类型修改] WHERE [合同号] IS NOT NULL AND [排产编号] IS NOT NULL AND [货号] IS NOT NULL GROUP BY [合同号], [排产编号], [货号] HAVING COUNT(*) > 1)
BEGIN RAISERROR(N'生产类型修改存在重复业务键，请先清理。', 16, 1); ROLLBACK TRANSACTION; RETURN; END;
IF EXISTS (SELECT 1 FROM [工单销控表] WHERE [货号] IS NOT NULL GROUP BY [货号] HAVING COUNT(*) > 1)
BEGIN RAISERROR(N'工单销控表存在重复货号，请先清理。', 16, 1); ROLLBACK TRANSACTION; RETURN; END;
IF EXISTS (SELECT 1 FROM [工单销控表明细] WHERE [货号] IS NOT NULL AND [分析单号] IS NOT NULL GROUP BY [货号], [分析单号] HAVING COUNT(*) > 1)
BEGIN RAISERROR(N'工单销控表明细存在重复业务键，请先清理。', 16, 1); ROLLBACK TRANSACTION; RETURN; END;
IF EXISTS (SELECT 1 FROM [外产_发运] WHERE [分析单号] IS NOT NULL AND [货号] IS NOT NULL GROUP BY [分析单号], [货号] HAVING COUNT(*) > 1)
BEGIN RAISERROR(N'外产_发运存在重复业务键，请先清理。', 16, 1); ROLLBACK TRANSACTION; RETURN; END;
IF EXISTS (SELECT 1 FROM [外产_生产] WHERE [分析单号] IS NOT NULL AND [货号] IS NOT NULL GROUP BY [分析单号], [货号] HAVING COUNT(*) > 1)
BEGIN RAISERROR(N'外产_生产存在重复业务键，请先清理。', 16, 1); ROLLBACK TRANSACTION; RETURN; END;
IF EXISTS (SELECT 1 FROM [排产分析单] WHERE [分析单号] IS NOT NULL GROUP BY [分析单号] HAVING COUNT(*) > 1)
BEGIN RAISERROR(N'排产分析单存在重复分析单号，请先清理。', 16, 1); ROLLBACK TRANSACTION; RETURN; END;

-- SQL Server 2008 limits an index key to 900 bytes. These legacy columns were
-- created wider than their business domain requires, so validate first and
-- narrow only the columns that must participate in an index key.
IF EXISTS (SELECT 1 FROM [生产类型修改] WHERE DATALENGTH([货号]) > 400)
    RAISERROR(N'生产类型修改.货号存在超过 200 个 Unicode 字符的数据，停止升级。', 16, 1);
IF EXISTS (SELECT 1 FROM [工单销控表] WHERE DATALENGTH([货号]) > 400)
    RAISERROR(N'工单销控表.货号存在超过 200 个 Unicode 字符的数据，停止升级。', 16, 1);
IF EXISTS (SELECT 1 FROM [工单销控表明细] WHERE DATALENGTH([货号]) > 400 OR DATALENGTH([父级编号]) > 400)
    RAISERROR(N'工单销控表明细的货号或父级编号存在超过 200 个 Unicode 字符的数据，停止升级。', 16, 1);

IF COL_LENGTH(N'生产类型修改', N'货号') > 400 ALTER TABLE [生产类型修改] ALTER COLUMN [货号] nvarchar(200) NULL;
IF COL_LENGTH(N'工单销控表', N'货号') > 400 ALTER TABLE [工单销控表] ALTER COLUMN [货号] nvarchar(200) NULL;
IF COL_LENGTH(N'工单销控表明细', N'货号') > 400 ALTER TABLE [工单销控表明细] ALTER COLUMN [货号] nvarchar(200) NULL;
IF COL_LENGTH(N'工单销控表明细', N'父级编号') > 400 ALTER TABLE [工单销控表明细] ALTER COLUMN [父级编号] nvarchar(200) NULL;

IF COL_LENGTH(N'外产_订单', N'RowVersion') IS NULL ALTER TABLE [外产_订单] ADD [RowVersion] rowversion NOT NULL;
IF COL_LENGTH(N'生产类型修改', N'RowVersion') IS NULL ALTER TABLE [生产类型修改] ADD [RowVersion] rowversion NOT NULL;
IF COL_LENGTH(N'工单销控表', N'RowVersion') IS NULL ALTER TABLE [工单销控表] ADD [RowVersion] rowversion NOT NULL;
IF COL_LENGTH(N'工单销控表明细', N'RowVersion') IS NULL ALTER TABLE [工单销控表明细] ADD [RowVersion] rowversion NOT NULL;
IF COL_LENGTH(N'外产_发运', N'RowVersion') IS NULL ALTER TABLE [外产_发运] ADD [RowVersion] rowversion NOT NULL;
IF COL_LENGTH(N'外产_生产', N'RowVersion') IS NULL ALTER TABLE [外产_生产] ADD [RowVersion] rowversion NOT NULL;
IF COL_LENGTH(N'外产_领料', N'RowVersion') IS NULL ALTER TABLE [外产_领料] ADD [RowVersion] rowversion NOT NULL;
IF COL_LENGTH(N'外产_入库', N'RowVersion') IS NULL ALTER TABLE [外产_入库] ADD [RowVersion] rowversion NOT NULL;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'[生产类型修改]') AND name=N'UX_生产类型修改_业务键') CREATE UNIQUE INDEX [UX_生产类型修改_业务键] ON [生产类型修改]([合同号],[排产编号],[货号]) WHERE [合同号] IS NOT NULL AND [排产编号] IS NOT NULL AND [货号] IS NOT NULL;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'[工单销控表]') AND name=N'UX_工单销控表_货号') CREATE UNIQUE INDEX [UX_工单销控表_货号] ON [工单销控表]([货号]) WHERE [货号] IS NOT NULL;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'[工单销控表明细]') AND name=N'UX_工单销控表明细_业务键') CREATE UNIQUE INDEX [UX_工单销控表明细_业务键] ON [工单销控表明细]([货号],[分析单号]) WHERE [货号] IS NOT NULL AND [分析单号] IS NOT NULL;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'[外产_发运]') AND name=N'UX_外产发运_业务键') CREATE UNIQUE INDEX [UX_外产发运_业务键] ON [外产_发运]([分析单号],[货号]) WHERE [分析单号] IS NOT NULL AND [货号] IS NOT NULL;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'[外产_生产]') AND name=N'UX_外产生产_业务键') CREATE UNIQUE INDEX [UX_外产生产_业务键] ON [外产_生产]([分析单号],[货号]) WHERE [分析单号] IS NOT NULL AND [货号] IS NOT NULL;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'[排产分析单]') AND name=N'UX_排产分析单_分析单号') CREATE UNIQUE INDEX [UX_排产分析单_分析单号] ON [排产分析单]([分析单号]) WHERE [分析单号] IS NOT NULL;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'[外产_订单]') AND name=N'IX_外产订单_状态排产货号') CREATE INDEX [IX_外产订单_状态排产货号] ON [外产_订单]([状态],[排产编号],[货号]);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'[外销合同产品]') AND name=N'IX_外销合同产品_货号合同分析层') CREATE INDEX [IX_外销合同产品_货号合同分析层] ON [外销合同产品]([货号],[合同号],[分析单号],[层]);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'[外销合同客户产品]') AND name=N'IX_外销合同客户产品_创建合同货号') CREATE INDEX [IX_外销合同客户产品_创建合同货号] ON [外销合同客户产品]([创建时间],[合同号],[货号]);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'[工单销控表明细]') AND name=N'IX_工单销控明细_父级') CREATE INDEX [IX_工单销控明细_父级] ON [工单销控表明细]([父级编号]);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'[外产_BOM]') AND name=N'IX_外产BOM_分析号') CREATE INDEX [IX_外产BOM_分析号] ON [外产_BOM]([分析单号]) INCLUDE ([货号],[父级编号]);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'[外产_领料]') AND name=N'IX_外产领料_分析货号') CREATE INDEX [IX_外产领料_分析货号] ON [外产_领料]([分析单号],[货号]);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'[外产_入库]') AND name=N'IX_外产入库_分析货号') CREATE INDEX [IX_外产入库_分析货号] ON [外产_入库]([分析单号],[货号]);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'[外产_发运]') AND name=N'IX_外产发运_排产货号') CREATE INDEX [IX_外产发运_排产货号] ON [外产_发运]([排产编号],[货号]);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'[外产_生产]') AND name=N'IX_外产生产_排产货号') CREATE INDEX [IX_外产生产_排产货号] ON [外产_生产]([排产编号],[货号]);

COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
    IF @PreviousCompatibilityLevel < 100
    BEGIN
        SET @CompatibilitySql = N'ALTER DATABASE ' + QUOTENAME(DB_NAME()) + N' SET COMPATIBILITY_LEVEL = ' + CONVERT(nvarchar(3), @PreviousCompatibilityLevel);
        EXEC(@CompatibilitySql);
    END;

    DECLARE @ErrorMessage nvarchar(4000), @ErrorSeverity int, @ErrorState int;
    SELECT @ErrorMessage = ERROR_MESSAGE(), @ErrorSeverity = ERROR_SEVERITY(), @ErrorState = ERROR_STATE();
    RAISERROR(@ErrorMessage, @ErrorSeverity, @ErrorState);
END CATCH;
