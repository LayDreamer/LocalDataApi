SET XACT_ABORT ON;
BEGIN TRANSACTION;

IF EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'[外产_订单]') AND name=N'IX_外产订单_状态排产货号') DROP INDEX [IX_外产订单_状态排产货号] ON [外产_订单];
IF EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'[外销合同产品]') AND name=N'IX_外销合同产品_货号合同分析层') DROP INDEX [IX_外销合同产品_货号合同分析层] ON [外销合同产品];
IF EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'[外销合同客户产品]') AND name=N'IX_外销合同客户产品_创建合同货号') DROP INDEX [IX_外销合同客户产品_创建合同货号] ON [外销合同客户产品];
IF EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'[工单销控表明细]') AND name=N'IX_工单销控明细_父级') DROP INDEX [IX_工单销控明细_父级] ON [工单销控表明细];
IF EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'[外产_BOM]') AND name=N'IX_外产BOM_分析号') DROP INDEX [IX_外产BOM_分析号] ON [外产_BOM];
IF EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'[外产_领料]') AND name=N'IX_外产领料_分析货号') DROP INDEX [IX_外产领料_分析货号] ON [外产_领料];
IF EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'[外产_入库]') AND name=N'IX_外产入库_分析货号') DROP INDEX [IX_外产入库_分析货号] ON [外产_入库];
IF EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'[外产_发运]') AND name=N'IX_外产发运_排产货号') DROP INDEX [IX_外产发运_排产货号] ON [外产_发运];
IF EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'[外产_生产]') AND name=N'IX_外产生产_排产货号') DROP INDEX [IX_外产生产_排产货号] ON [外产_生产];

IF EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'[生产类型修改]') AND name=N'UX_生产类型修改_业务键') DROP INDEX [UX_生产类型修改_业务键] ON [生产类型修改];
IF EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'[工单销控表]') AND name=N'UX_工单销控表_货号') DROP INDEX [UX_工单销控表_货号] ON [工单销控表];
IF EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'[工单销控表明细]') AND name=N'UX_工单销控表明细_业务键') DROP INDEX [UX_工单销控表明细_业务键] ON [工单销控表明细];
IF EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'[外产_发运]') AND name=N'UX_外产发运_业务键') DROP INDEX [UX_外产发运_业务键] ON [外产_发运];
IF EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'[外产_生产]') AND name=N'UX_外产生产_业务键') DROP INDEX [UX_外产生产_业务键] ON [外产_生产];
IF EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'[排产分析单]') AND name=N'UX_排产分析单_分析单号') DROP INDEX [UX_排产分析单_分析单号] ON [排产分析单];

IF COL_LENGTH(N'外产_订单', N'RowVersion') IS NOT NULL ALTER TABLE [外产_订单] DROP COLUMN [RowVersion];
IF COL_LENGTH(N'生产类型修改', N'RowVersion') IS NOT NULL ALTER TABLE [生产类型修改] DROP COLUMN [RowVersion];
IF COL_LENGTH(N'工单销控表', N'RowVersion') IS NOT NULL ALTER TABLE [工单销控表] DROP COLUMN [RowVersion];
IF COL_LENGTH(N'工单销控表明细', N'RowVersion') IS NOT NULL ALTER TABLE [工单销控表明细] DROP COLUMN [RowVersion];
IF COL_LENGTH(N'外产_发运', N'RowVersion') IS NOT NULL ALTER TABLE [外产_发运] DROP COLUMN [RowVersion];
IF COL_LENGTH(N'外产_生产', N'RowVersion') IS NOT NULL ALTER TABLE [外产_生产] DROP COLUMN [RowVersion];
IF COL_LENGTH(N'外产_领料', N'RowVersion') IS NOT NULL ALTER TABLE [外产_领料] DROP COLUMN [RowVersion];
IF COL_LENGTH(N'外产_入库', N'RowVersion') IS NOT NULL ALTER TABLE [外产_入库] DROP COLUMN [RowVersion];

IF COL_LENGTH(N'生产类型修改', N'货号') = 400 ALTER TABLE [生产类型修改] ALTER COLUMN [货号] nvarchar(500) NULL;
IF COL_LENGTH(N'工单销控表', N'货号') = 400 ALTER TABLE [工单销控表] ALTER COLUMN [货号] nvarchar(500) NULL;
IF COL_LENGTH(N'工单销控表明细', N'货号') = 400 ALTER TABLE [工单销控表明细] ALTER COLUMN [货号] nvarchar(500) NULL;
IF COL_LENGTH(N'工单销控表明细', N'父级编号') = 400 ALTER TABLE [工单销控表明细] ALTER COLUMN [父级编号] nvarchar(500) NULL;

COMMIT TRANSACTION;

IF (SELECT compatibility_level FROM sys.databases WHERE name = DB_NAME()) = 100
BEGIN
    DECLARE @CompatibilitySql nvarchar(4000);
    SET @CompatibilitySql = N'ALTER DATABASE ' + QUOTENAME(DB_NAME()) + N' SET COMPATIBILITY_LEVEL = 80';
    EXEC(@CompatibilitySql);
END;
